/*
Copyright 2012, 2013 Gnoso Inc.

This software is licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except for what is in compliance with the License.

You may obtain a copy of this license at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either expressed or implied.

See the License for the specific language governing permissions and limitations.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using System.Diagnostics;

namespace RazorDBx
{
	public enum ExceptionHandling
	{
		ThrowAll,
		AttemptRecovery
	}

	public class KeyValueStore : IDisposable
	{
		public KeyValueStore (string baseFileName) : this(baseFileName, (RazorCache)null)
		{
		}

		public KeyValueStore (string baseFileName, RazorCache cache) // Initializes a new instance of the KeyValueStore.
		{
			if (!Directory.Exists (baseFileName))
				Directory.CreateDirectory (baseFileName);
			_manifest = new Manifest (baseFileName);
			_manifest.Logger = RazorDBx.C5.Logger.Log;

			int memTableVersion = _manifest.CurrentVersion (0);

			CheckForIncompleteJournalRotation (baseFileName, memTableVersion); // Check for a previously aborted journal rotation.

			_currentJournaledMemTable = new JournaledMemTable (_manifest.BaseFileName, memTableVersion); // Create new journal for this run (and potentially load from disk, if there was data loaded previously).
			_cache = cache == null ? new RazorCache () : cache;
		}

		bool finalizing = false;

		~KeyValueStore ()
		{
			finalizing = true;
			Dispose ();
		}

		Manifest _manifest;
		RazorCache _cache;
		Dictionary<string, KeyValueStore> _secondaryIndexes = new Dictionary<string, KeyValueStore> (StringComparer.OrdinalIgnoreCase);
		ExceptionHandling _exceptionHandling = ExceptionHandling.ThrowAll;
		// For the table manager
		internal long ticksTillNextMerge = 0;
		internal object mergeLock = new object ();
		internal int mergeCount = 0;

		public Manifest Manifest { // Gets the manifest.
			get {
				return _manifest;
			}
		}

		public Action<int, IEnumerable<PageRecord>, IEnumerable<PageRecord>> MergeCallback { // Gets or sets the merge callback.
			get;
			set;
		}

		internal RazorCache Cache { // Gets the cache.
			get {
				return _cache;
			}
		}

		public int DataCacheSize { // Gets the size of the data cache.
			get {
				return Cache.DataCacheSize;
			}
		}

		public int IndexCacheSize { // Gets the size of the index cache.
			get {
				return Cache.IndexCacheSize;
			}
		}

		volatile JournaledMemTable _currentJournaledMemTable;

		public void Truncate () // Truncates a current database.
		{
			_currentJournaledMemTable.Close ();
			TableManager.Default.Close (this);
			foreach (var pair in _secondaryIndexes)
				pair.Value.Close (FastClose);

			string basePath = Path.GetFullPath (Manifest.BaseFileName);
			foreach (string file in Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories))
				File.Delete (file);
			foreach (string dir in Directory.GetDirectories(basePath, "*.*", SearchOption.AllDirectories))
				Directory.Delete (dir, true);

			_manifest = new Manifest (basePath);
			_currentJournaledMemTable = new JournaledMemTable (_manifest.BaseFileName, _manifest.CurrentVersion (0));
			_cache = new RazorCache ();
			_secondaryIndexes = new Dictionary<string, KeyValueStore> (StringComparer.OrdinalIgnoreCase);

			Manifest.LogMessage ("Database Truncated.");
		}

		public void Set (byte[] key, byte[] value) // Set the specified key and value.
		{
			Set (key, value, null);
		}

		public object multiPageLock = new object ();
		// The multi page lock.
		public void Set (byte[] key, byte[] value, IDictionary<string, byte[]> indexedValues) // Set the specified key, value and indexedValues.
		{
			int valueSize = value.Length;
			if (valueSize <= Config.MaxSmallValueSize) {
				var k = new Key (key, 0);
				var v = new Value (value, ValueFlag.SmallValue);
				InternalSet (k, v, indexedValues);
			} else {
				lock (multiPageLock) {
					if (value.Length >= Config.MaxLargeValueSize)
						throw new InvalidDataException (string.Format ("Value is larger than the maximum size. ({0} bytes)", Config.MaxLargeValueSize));

					int offset = 0;
					byte seqNum = 1;

					while (offset < valueSize) {
						var k = new Key (key, seqNum);
						int length = Math.Min (valueSize - offset, Config.MaxSmallValueSize);
						var v = new Value (ByteArray.From (value, offset, length).InternalBytes, ValueFlag.LargeValueChunk);
						InternalSet (k, v, null);
						offset += length;
						seqNum++;
					}

					var dk = new Key (key, 0);
					var dv = new Value (BitConverter.GetBytes (valueSize), ValueFlag.LargeValueDescriptor);
					InternalSet (dk, dv, indexedValues);
				}
			}
		}

		public void RemoveFromIndex (byte[] key, IDictionary<string, byte[]> indexedValues) // Removes the index of the from.
		{
			foreach (var pair in indexedValues) {
				string IndexName = pair.Key;

				// Construct Index key by concatenating the indexed value and the target key
				byte[] indexValue = pair.Value;
				byte[] indexKey = new byte[key.Length + indexValue.Length];
				indexValue.CopyTo (indexKey, 0);
				key.CopyTo (indexKey, indexValue.Length);

				KeyValueStore indexStore = GetSecondaryIndex (IndexName);
				indexStore.Delete (indexKey);
			}
		}

		public void RemoveIndexRangeForValue (string indexName, byte[] startAt, byte[] value)
		{
			KeyValueStore indexStore = GetSecondaryIndex (indexName);
			var pairs = indexStore.EnumerateFromKey (startAt);
			foreach (var pair in pairs) {
				if (ByteArray.CompareMemCmp (pair.Value, value) == 0)
					indexStore.Delete (pair.Key);
				if (ByteArray.CompareMemCmp (startAt, 0, pair.Key, 0, startAt.Length) == 0)
					continue; // Exit index removal only when the projectID in the index key changes.
				break;
			}
		}

		public void CleanIndex (string indexName) // Cleans the index.
		{
			KeyValueStore indexStore = GetSecondaryIndex (indexName);

			var allValueStoreItems = new HashSet<ByteArray> (this.Enumerate ().Select (item => new ByteArray (item.Key)));
			foreach (var indexItem in indexStore.Enumerate())
				if (!allValueStoreItems.Contains (new ByteArray (indexItem.Value)))
					indexStore.Delete (indexItem.Key);
		}

		public void DropIndex (string indexName)
		{
			KeyValueStore indexStore = GetSecondaryIndex (indexName);
			if (indexStore != null)
				indexStore.Truncate ();
		}

		void InternalSet (Key k, Value v, IDictionary<string, byte[]> indexedValues)
		{
			int adds = 10;
			while (!_currentJournaledMemTable.Add(k, v)) {
				adds--;
				if (adds <= 0)
					throw new InvalidOperationException ("Failed too many times trying to add an item to the JournaledMemTable");
			}
			// Add secondary index values if they were provided
			if (indexedValues != null)
				AddToIndex (k.KeyBytes, indexedValues);

			if (_currentJournaledMemTable.Full)
				RotateMemTable ();

			TableManager.Default.MarkKeyValueStoreAsModified (this);
		}

		public void AddToIndex (byte[] key, IDictionary<string, byte[]> indexedValues) // Adds an index to a specified value.
		{
			foreach (var pair in indexedValues) {
				string IndexName = pair.Key;

				// Construct Index key by concatenating the indexed value and the target key
				byte[] indexValue = pair.Value;
				byte[] indexKey = new byte[key.Length + indexValue.Length];
				indexValue.CopyTo (indexKey, 0);
				key.CopyTo (indexKey, indexValue.Length);

				KeyValueStore indexStore = GetSecondaryIndex (IndexName);
				indexStore.Set (indexKey, key);
			}
		}

		KeyValueStore GetSecondaryIndex (string IndexName)
		{
			KeyValueStore indexStore = null;
			lock (_secondaryIndexes) {
				if (!_secondaryIndexes.TryGetValue (IndexName, out indexStore)) {
					indexStore = new KeyValueStore (Config.IndexBaseName (Manifest.BaseFileName, IndexName), _cache);
					if (Manifest.Logger != null)
						indexStore.Manifest.Logger = msg => Manifest.Logger (string.Format ("{0}: {1}", IndexName, msg));
					_secondaryIndexes.Add (IndexName, indexStore);
				}
			}
			return indexStore;
		}

		public byte[] Get (byte[] key) // Get the specified key.
		{
			Key lookupKey = new Key (key, 0);
			return AssembleGetResult (lookupKey, InternalGet (lookupKey));
		}

		Value InternalGet (Key lookupKey)
		{
			Value output = Value.Empty;

			var rotatedMemTable = _rotatedJournaledMemTable; // Capture copy of the rotated table if there is one.

			if (_currentJournaledMemTable.Lookup (lookupKey, out output))
				return output; // Check the current memtable.
			if (rotatedMemTable != null)
			if (rotatedMemTable.Lookup (lookupKey, out output))
				return output; // Check the table in rotation.

			using (var manifest = _manifest.GetLatestManifest()) { // Check the files on disk.
				// Must check all pages on level 0.
				var zeroPages = manifest.GetPagesAtLevel (0).OrderByDescending ((page) => page.Version);
				foreach (var page in zeroPages)
					if (SortedBlockTable.Lookup (_manifest.BaseFileName, page.Level, page.Version, _cache, lookupKey, out output, _exceptionHandling, _manifest.Logger))
						return output;
				// If not found, must check pages on the higher levels, but we can use the page index to make the search quicker.
				for (int level = 1; level < manifest.NumLevels; level++) {
					var page = manifest.FindPageForKey (level, lookupKey);
					if (page != null && SortedBlockTable.Lookup (_manifest.BaseFileName, page.Level, page.Version, _cache, lookupKey, out output, _exceptionHandling, _manifest.Logger))
						return output;
				}
			}
			return Value.Empty; // Returns null.
		}

		byte[] AssembleGetResult (Key lookupKey, Value result)
		{
			switch (result.Type) {
			case ValueFlag.Null:
			case ValueFlag.Deleted:
				return null;
			case ValueFlag.SmallValue:
				return result.ValueBytes;
			case ValueFlag.LargeValueDescriptor:
				{
					lock (multiPageLock) {
						result = InternalGet (lookupKey); // read the descriptor again in case it changed
						if (result.Type == ValueFlag.LargeValueDescriptor) { // make sure type is still large value descriptor and continue
							int valueSize = BitConverter.ToInt32 (result.ValueBytes, 0);
							byte[] bytes = new byte[valueSize];
							int offset = 0;
							byte seqNum = 1;
							while (offset < valueSize) {
								var blockKey = lookupKey.WithSequence (seqNum);
								var block = InternalGet (blockKey);
								if (block.Type != ValueFlag.LargeValueChunk)
									throw new InvalidDataException (string.Format ("Corrupted data: block is missing. Block Type: {0} SeqNum: {1}, Block Key: {2}", block.Type, seqNum, blockKey));
								offset += block.CopyValueBytesTo (bytes, offset);
								seqNum++;
							}
							return bytes;
						} else
							return AssembleGetResult (lookupKey, result);
					}
				}
			default:
				throw new InvalidOperationException ("Unexpected value flag for result.");
			}
		}

		public int CountIndex (string indexName)
		{
			KeyValueStore indexStore = GetSecondaryIndex (indexName);
			return indexStore.Enumerate ().Count ();
		}

		public IEnumerable<KeyValuePair<byte[], byte[]>> Find (string indexName, byte[] lookupValue) // Find the specified indexName and lookupValue.
		{
			KeyValueStore indexStore = GetSecondaryIndex (indexName);

			foreach (var pair in indexStore.EnumerateFromKey(lookupValue)) { // Loop over the values
				var key = pair.Key;
				var value = pair.Value;

				// construct our index key pattern (lookupvalue | key), then lookup the value of the actual object using the key that was found; if the previous condition was not met, then we must have enumerated past the end of the indexed value
				if (ByteArray.CompareMemCmp (key, 0, lookupValue, 0, lookupValue.Length) == 0)
				if (key.Length == (value.Length + lookupValue.Length) && ByteArray.CompareMemCmp (key, lookupValue.Length, value, 0, value.Length) == 0)
				if (Get (value) != null)
					yield return new KeyValuePair<byte[], byte[]> (value, Get (value));
				else
					yield break;
			}
		}

		public IEnumerable<KeyValuePair<byte[], byte[]>> FindStartsWith (string indexName, byte[] lookupValue)
		{
			KeyValueStore indexStore = GetSecondaryIndex (indexName);

			foreach (var pair in indexStore.EnumerateFromKey(lookupValue)) { // Loops over the values.
				var key = pair.Key;
				var value = pair.Value;

				// Constructs the index key pattern (lookupvalue | key), then looks up the value of the actual object using the discovered key: If the previous condition was not met, then we must have enumerated past the end of the indexed value.
				if (ByteArray.CompareMemCmp (key, 0, lookupValue, 0, lookupValue.Length) == 0)
				if (key.Length >= (value.Length + lookupValue.Length))
				if (Get (value) != null)
					yield return new KeyValuePair<byte[], byte[]> (value, Get (value));
				else
					yield break;
			}
		}

		public void Delete (byte[] key) // Delete the specified key.
		{
			var k = new Key (key, 0);
			InternalSet (k, Value.Deleted, null);
		}

		public IEnumerable<KeyValuePair<byte[], byte[]>> Enumerate () // Enumerate this instance.
		{
			return EnumerateFromKey (new byte[0]);
		}

		IEnumerable<KeyValuePair<Key, Value>> InternalEnumerateFromKey (byte[] startingKey)
		{
			if (startingKey == null)
				yield break;

			var enumerators = new List<IEnumerable<KeyValuePair<Key, Value>>> ();
			Key key = new Key (startingKey, 0);

			var rotatedMemTable = _rotatedJournaledMemTable; // Captures a copy of the rotated table if one exists.
			enumerators.Add (_currentJournaledMemTable.EnumerateSnapshotFromKey (key)); // Selects the main MemTable.

			if (rotatedMemTable != null)
				enumerators.Add (rotatedMemTable.EnumerateSnapshotFromKey (key));

			// Checks the files on disk.
			using (var manifestSnapshot = _manifest.GetLatestManifest()) {
				List<SortedBlockTable> tables = new List<SortedBlockTable> ();
				try {
					for (int i = 0; i < manifestSnapshot.NumLevels; i++)
						tables.AddRange (manifestSnapshot.GetPagesAtLevel (i).OrderByDescending (page => page.Version).Select (page => new SortedBlockTable (_cache, _manifest.BaseFileName, page.Level, page.Version)));
					enumerators.AddRange (tables.Select (t => t.EnumerateFromKey (_cache, key)));
					foreach (var pair in MergeEnumerator.Merge(enumerators, t => t.Key))
						yield return pair;
				} finally {
					tables.ForEach (table => table.Close ()); // Closes all tables.
				}
			}
		}

		public IEnumerable<KeyValuePair<byte[], byte[]>> EnumerateFromKey (byte[] startingKey) // Enumerates from key.
		{
			foreach (var pair in InternalEnumerateFromKey(startingKey)) {
				if (pair.Key.SequenceNum == 0) { // only enumerate top-level keys (sequence zero)
					byte[] result = AssembleGetResult (pair.Key, pair.Value);
					if (result != null)
						yield return new KeyValuePair<byte[], byte[]> (pair.Key.KeyBytes, result);
				}
			}
		}

		object memTableRotationLock = new object ();
		JournaledMemTable _rotatedJournaledMemTable;
		Semaphore _rotationSemaphore = new Semaphore (1, 1);
		// Rotates the mem table.
		public void RotateMemTable ()
		{
			lock (memTableRotationLock) {
				if (_currentJournaledMemTable.Full) { // Double check the flag in case we have multiple threads that make it into this routine.
					_rotationSemaphore.WaitOne (); // Wait for the rotation gate to be open, and automatically reset once a single thread gets through.
					_rotatedJournaledMemTable = Interlocked.Exchange<JournaledMemTable> (ref _currentJournaledMemTable, new JournaledMemTable (_manifest.BaseFileName, _manifest.NextVersion (0)));

					ThreadPool.QueueUserWorkItem ((o) => {
						try {
							_rotatedJournaledMemTable.WriteToSortedBlockTable (_manifest);
							_rotatedJournaledMemTable = null;
						} finally {
							_rotationSemaphore.Release (); // Open the gate for the next rotation.
						}
					});
				}
			}
		}

		void CheckForIncompleteJournalRotation (string baseFileName, int currentMemTableVersion)
		{
			int previousMemTableVersion = currentMemTableVersion - 1;
			if (File.Exists (Config.JournalFile (baseFileName, previousMemTableVersion))) { // Is there a left-over journal from a previous rotation that was aborted while in rotation?
				var memTable = new JournaledMemTable (baseFileName, previousMemTableVersion);
				memTable.WriteToSortedBlockTable (_manifest);
				memTable.Close ();
			}
		}
		/*
		Releases all resource used by the KeyValueStore object.

		Call Dispose() when you are finished using the KeyValueStore.
		The Dispose() method leaves the KeyValueStore in an unusable state.
		After calling Dispose(), you must release all references to the KeyValueStore so the garbage collector can reclaim the memory that the KeyValueStore was occupying.
		*/
		public void Dispose ()
		{
			Close (FastClose);
		}

		bool _fastClose = false;

		public bool FastClose { // Gets or sets a value indicating whether this KeyValueStore closes fast.
			get {
				return _fastClose;
			}
			set {
				_fastClose = value;
			}
		}

		public void Close (bool fast = false) // Close a specified value.
		{
			// Make sure any inflight rotations have occurred before shutting down.
			if (!_rotationSemaphore.WaitOne (30000))
				throw new TimeoutException ("Timed out waiting for table rotation to complete.");
			_rotationSemaphore.Release (); // Release again in case another thread tries to close it again.

			if (!finalizing && !fast)
				TableManager.Default.Close (this);
			if (_currentJournaledMemTable != null) {
				_currentJournaledMemTable.Close ();
				_currentJournaledMemTable = null;
			}

			if (_secondaryIndexes != null)
				foreach (var idx in _secondaryIndexes)
					idx.Value.Close (fast);

			GC.SuppressFinalize (this); // Don't finalize since we already closed it.
		}

		public void ScanCheck () // Does a quick check.
		{
			long totalKeyBytes = 0;
			long totalValueBytes = 0;
			int totalRecords = 0;
			int deletedRecords = 0;
			int nullRecords = 0;
			int smallRecords = 0;
			int largeDescRecords = 0;
			int largeChunkRecords = 0;
			try {
				foreach (var pair in InternalEnumerateFromKey(new byte[0])) {
					try {
						Key k = pair.Key;
						Value v = pair.Value;

						totalKeyBytes += k.KeyBytes.Length;
						totalValueBytes += v.ValueBytes.Length;
						totalRecords += 1;
						switch (v.Type) {
						case ValueFlag.Null:
							nullRecords += 1;
							break;
						case ValueFlag.Deleted:
							deletedRecords += 1;
							break;
						case ValueFlag.SmallValue:
							smallRecords += 1;
							break;
						case ValueFlag.LargeValueDescriptor:
							largeDescRecords += 1;
							break;
						case ValueFlag.LargeValueChunk:
							largeChunkRecords += 1;
							break;
						default:
							throw new ApplicationException ("Unknown Value Type");
						}
						if (v.Type == ValueFlag.LargeValueDescriptor) {
							var lookupKey = k;
							int valueSize = BitConverter.ToInt32 (v.ValueBytes, 0);
							int offset = 0;
							byte seqNum = 1;
							while (offset < valueSize) {
								var blockKey = lookupKey.WithSequence (seqNum);
								var block = InternalGet (blockKey);
								if (block.Type != ValueFlag.LargeValueChunk)
									throw new InvalidDataException (string.Format ("Corrupted data: block is missing. Block Type: {0} SeqNum: {1}, Block Key: {2}", block.Type, seqNum, blockKey));
								offset += block.Length - 1;
								seqNum++;
							}
							if (offset != valueSize)
								throw new InvalidDataException (string.Format ("Chunk sizes ({0}) are different from the descriptor size ({1}).", offset, valueSize));
						}
					} catch (Exception ex) {
						Console.WriteLine ("**Error Reading Record: {0}", ex);
					}
				}
			} catch (Exception ex) {
				Console.WriteLine ("**Error Enumerating File: {0}", ex);
			} finally {
				Console.WriteLine ("  KeyBytes: {0}, ValueBytes: {1}\n  Records: {2} Deleted: {3} Null: {4} Small: {5} LargeDesc: {6} LargeChunk: {7}", totalKeyBytes, totalValueBytes, totalRecords, deletedRecords, nullRecords, smallRecords, largeDescRecords, largeChunkRecords);
			}
		}
		// List all the pages in the directory and delete those that are not in the manifest.
		public void RemoveOrphanedPages ()
		{
			using (var manifestInst = this.Manifest.GetLatestManifest()) {
				// Finds all of the sbt files in the data directory.
				var files = Directory.GetFiles (this.Manifest.BaseFileName, "*.sbt").ToDictionary (f => Path.GetFileNameWithoutExtension (f.ToLower ()));

				for (int level = 0; level < manifestInst.NumLevels - 1; level++) {
					foreach (var page in manifestInst.GetPagesAtLevel(level)) {
						// Adds the file path and then strips it off (uses the exact same logic as that which creates the file names).
						string fileForPage = Path.GetFileNameWithoutExtension (Config.SortedBlockTableFile (this.Manifest.BaseFileName, page.Level, page.Version));
						files.Remove (fileForPage); // Remove the page from the file list because it's in the manifest and we've accounted for it.
					}
				}

				foreach (var file in files.Keys) { // Loop over the leftover files and handle them.
					try {
						var parts = file.Split ('-');
						int level = int.Parse (parts [0]);
						int version = int.Parse (parts [1]);

						// First let's check the version number, we don't want to remove any new files that are being created while this is happening.
						if (level < manifestInst.NumLevels && version < manifestInst.CurrentVersion (level)) {
							string orphanedFile = Config.SortedBlockTableFile (Manifest.BaseFileName, level, version);
							File.Delete (orphanedFile); // Deletes the orphaned file.

							Manifest.LogMessage ("Removing Orphaned Pages '{0}'", orphanedFile);
						}
					} catch {
						// Best effort, here. If we fail, continue.
					}
				}
			}

			Manifest.LogMessage ("Removing Orphaned Index Pages"); // Processes the indexes.

			lock (_secondaryIndexes)
				foreach (var idx in _secondaryIndexes)
					idx.Value.RemoveOrphanedPages ();
		}
	}
}