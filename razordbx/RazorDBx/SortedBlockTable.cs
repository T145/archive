﻿/*
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
using System.Text;
using System.IO;
using System.Threading;

namespace RazorDBx
{
	public enum RecordHeaderFlag
	{
		Record = 0xE0,
		EndOfBlock = 0xFF
	};

	public class SortedBlockTableWriter // With this implementation, the maximum sized data that can be stored is ... block size >= keylen + valuelen + (sizecounter - no more than 8 bytes)
	{
		public SortedBlockTableWriter (string baseFileName, int level, int version)
		{
			string fileName = Config.SortedBlockTableFile (baseFileName, level, version);
			_fileStream = new FileStream (fileName, FileMode.Create, FileAccess.Write, FileShare.None, Config.SortedBlockSize, Config.SortedBlockTableFileOptions);
			_bufferA = new byte[Config.SortedBlockSize];
			_bufferB = new byte[Config.SortedBlockSize];
			_buffer = _bufferA;
			_bufferPos = 0;
			_pageIndex = new List<Key> ();
			Version = version;
			WrittenSize = 0;
		}

		FileStream _fileStream;
		byte[] _bufferA;
		// pre-allocated bufferB
		byte[] _bufferB;
		// pre-allocated bufferA
		byte[] _buffer;
		// current buffer that is being loaded
		int _bufferPos;
		IAsyncResult _async;
		List<Key> _pageIndex;
		int dataBlocks = 0;
		int indexBlocks = 0;
		int totalBlocks = 0;

		public int Version {
			get;
			set;
		}

		public int WrittenSize {
			get;
			set;
		}

		void SwapBuffers ()
		{
			_buffer = Object.ReferenceEquals (_buffer, _bufferA) ? _bufferB : _bufferA;
			Array.Clear (_buffer, 0, _buffer.Length);
		}

		List<ushort> _keyOffsets = new List<ushort> ();

		public void WritePair (Key key, Value value) // Write a new key value pair to the output file. This method assumes that the data is being fed in key-sorted order.
		{
			byte[] keySize = new byte[8];
			int keySizeLen = Helper.Encode7BitInt (keySize, key.Length);
			byte[] valueSize = new byte[8];
			int valueSizeLen = Helper.Encode7BitInt (valueSize, value.Length);

			int bytesNeeded = keySizeLen + key.Length + valueSizeLen + value.Length + 4 + 1;

			if ((_bufferPos + bytesNeeded) > Config.SortedBlockSize)
				WriteDataBlock (); // Do we need to write out a block before adding this key value pair?

			if (_bufferPos == 0) { // If we are at the beginning of the buffer, then add this key to the index.
				_pageIndex.Add (key);
				_bufferPos += 2; // Add a place for the root node offset.
			}

			_keyOffsets.Add ((ushort)_bufferPos); // Store the pair in preparation for writing.
			_buffer [_bufferPos++] = (byte)RecordHeaderFlag.Record; // A record header.
			_bufferPos += 4; // Adds space for left and right node pointers.

			// Writes the data out to the buffer.
			Array.Copy (keySize, 0, _buffer, _bufferPos, keySizeLen);
			_bufferPos += keySizeLen;
			Array.Copy (key.InternalBytes, 0, _buffer, _bufferPos, key.Length);
			_bufferPos += key.Length;
			Array.Copy (valueSize, 0, _buffer, _bufferPos, valueSizeLen);
			_bufferPos += valueSizeLen;
			Array.Copy (value.InternalBytes, 0, _buffer, _bufferPos, value.Length);
			_bufferPos += value.Length;

			WrittenSize += bytesNeeded;
		}

		ushort BuildBlockTree (byte[] block, int startIndex, int endIndex, List<ushort> keyOffsets)
		{
			int middleIndex = (startIndex + endIndex) >> 1;
			ushort nodeOffset = keyOffsets [middleIndex];

			if (startIndex < middleIndex) { // Build left side
				ushort leftOffset = BuildBlockTree (block, startIndex, middleIndex - 1, keyOffsets);
				byte[] left = BitConverter.GetBytes (leftOffset);
				Array.Copy (left, 0, block, nodeOffset + 1, 2);
			}

			if (middleIndex < endIndex) { // Build right side
				ushort rightOffset = BuildBlockTree (block, middleIndex + 1, endIndex, keyOffsets);
				byte[] right = BitConverter.GetBytes (rightOffset);
				Array.Copy (right, 0, block, nodeOffset + 3, 2);
			}
			return nodeOffset;
		}

		void WriteDataBlock ()
		{
			if (_bufferPos < Config.SortedBlockSize)
				_buffer [_bufferPos++] = (byte)RecordHeaderFlag.EndOfBlock; // Write the end of buffer flag if we have room

			// Build the tree structure and fill in the starting pointer
			ushort middleTreePtr = BuildBlockTree (_buffer, 0, _keyOffsets.Count - 1, _keyOffsets);
			byte[] middleTree = BitConverter.GetBytes (middleTreePtr);
			Array.Copy (middleTree, _buffer, 2);
			_keyOffsets.Clear ();

			WriteBlock ();
		}

		void WriteBlock ()
		{
			if (_async != null)
				_fileStream.EndWrite (_async); // make sure any outstanding writes are completed
			_async = _fileStream.BeginWrite (_buffer, 0, Config.SortedBlockSize, null, null);
			SwapBuffers ();
			_bufferPos = 0;
			totalBlocks++;
		}

		void WriteIndexKey (Key key)
		{
			byte[] keySize = new byte[8];
			int keySizeLen = Helper.Encode7BitInt (keySize, key.Length);

			int bytesNeeded = keySizeLen + key.Length;

			if ((_bufferPos + bytesNeeded) > Config.SortedBlockSize)
				WriteBlock (); // Do we need to write out a block before adding this key value pair?

			// write the data out to the buffer
			Array.Copy (keySize, 0, _buffer, _bufferPos, keySizeLen);
			_bufferPos += keySizeLen;
			Array.Copy (key.InternalBytes, 0, _buffer, _bufferPos, key.Length);
			_bufferPos += key.Length;
		}

		void WriteIndex ()
		{
			foreach (var key in _pageIndex)
				WriteIndexKey (key);
		}

		void WriteMetadata ()
		{
			MemoryStream ms = new MemoryStream ();
			BinaryWriter writer = new BinaryWriter (ms);

			writer.Write (Encoding.ASCII.GetBytes ("@RAZORDB"));
			writer.Write7BitEncodedInt (totalBlocks + 1);
			writer.Write7BitEncodedInt (dataBlocks);
			writer.Write7BitEncodedInt (indexBlocks);

			byte[] metadata = ms.ToArray ();
			Array.Copy (metadata, _buffer, metadata.Length);

			// Commit the block to disk and wait for the operation to complete
			WriteBlock ();
			_fileStream.EndWrite (_async);
		}

		public void Close ()
		{
			if (_fileStream != null) {
				WriteDataBlock (); // Write the last block of the data
				dataBlocks = totalBlocks;
				WriteIndex (); // Write the Index entries
				WriteBlock (); // Write the last block of the index
				indexBlocks = totalBlocks - dataBlocks;
				WriteMetadata (); // Write metadata block at the end
				_fileStream.Close ();
			}
			_fileStream = null;
		}
	}

	public class SortedBlockTable
	{
		public SortedBlockTable (RazorCache cache, string baseFileName, int level, int version)
		{
			_baseFileName = baseFileName;
			_level = level;
			_version = version;
			_cache = cache;
			_path = Config.SortedBlockTableFile (baseFileName, level, version);
			ReadMetadata ();
		}

		string _path;
		bool FileExists = true;
		FileStream _fileStream;

		FileStream internalFileStream {
			get {
				if (!FileExists || _fileStream != null)
					return _fileStream;
				_fileStream = new FileStream (_path, FileMode.Open, FileAccess.Read, FileShare.Read, Config.SortedBlockSize, Config.SortedBlockTableFileOptions);
				return _fileStream;
			}
		}

		string _baseFileName;
		int _level;
		int _version;
		int _dataBlocks;
		int _indexBlocks;
		int _totalBlocks;
		RazorCache _cache;

		void SwapBlocks (byte[] blockA, byte[] blockB, ref byte[] current)
		{
			if (!FileExists)
				return;
			current = Object.ReferenceEquals (current, blockA) ? blockB : blockA; // swap the blocks so we can issue another disk i/o
			Array.Clear (current, 0, current.Length);
		}

		internal class AsyncBlock : IAsyncResult
		{
			internal byte[] Buffer;
			internal int BlockNum;

			public object AsyncState {
				get {
					return this;
				}
			}

			public WaitHandle AsyncWaitHandle {
				get {
					throw new NotImplementedException ();
				}
			}

			public bool CompletedSynchronously {
				get {
					return true;
				}
			}

			public bool IsCompleted {
				get {
					return true;
				}
			}
		}

		IAsyncResult BeginReadBlock (byte[] block, int blockNum)
		{
			if (!FileExists)
				return null;

			if (_cache != null) {
				byte[] cachedBlock = _cache.GetBlock (_baseFileName, _level, _version, blockNum);
				if (cachedBlock != null)
					return new AsyncBlock { Buffer = cachedBlock, BlockNum = blockNum };
			}
			internalFileStream.Seek (blockNum * Config.SortedBlockSize, SeekOrigin.Begin);
			return internalFileStream.BeginRead (block, 0, Config.SortedBlockSize, null, new AsyncBlock {
				Buffer = block,
				BlockNum = blockNum
			});
		}

		byte[] EndReadBlock (IAsyncResult async)
		{
			AsyncBlock ablock = async as AsyncBlock;
			if (ablock != null)
				return ablock.Buffer;
			else {
				internalFileStream.EndRead (async);
				ablock = (AsyncBlock)async.AsyncState;
				if (_cache != null) {
					var blockCopy = (byte[])ablock.Buffer.Clone ();
					_cache.SetBlock (_baseFileName, _level, _version, ablock.BlockNum, blockCopy);
				}
				return ablock.Buffer;
			}
		}

		byte[] ReadBlock (byte[] block, int blockNum)
		{
			if (_cache != null) {
				byte[] cachedBlock = _cache.GetBlock (_baseFileName, _level, _version, blockNum);
				if (cachedBlock != null)
					return cachedBlock;
			}
			internalFileStream.Seek (blockNum * Config.SortedBlockSize, SeekOrigin.Begin);
			internalFileStream.Read (block, 0, Config.SortedBlockSize);
			if (_cache != null) {
				var blockCopy = (byte[])block.Clone ();
				_cache.SetBlock (_baseFileName, _level, _version, blockNum, blockCopy);
			}
			return block;
		}

		[ThreadStatic]
		static byte[] threadAllocBlock = null;

		static byte[] LocalThreadAllocatedBlock ()
		{
			if (threadAllocBlock == null)
				threadAllocBlock = new byte[Config.SortedBlockSize];
			else
				Array.Clear (threadAllocBlock, 0, threadAllocBlock.Length);
			return threadAllocBlock;
		}

		void ReadMetadata ()
		{
			byte[] mdBlock = null;
			int numBlocks = -1;

			try {
				if (_cache != null) {
					mdBlock = _cache.GetBlock (_baseFileName, _level, _version, int.MaxValue);
					if (mdBlock == null) {
						numBlocks = (int)internalFileStream.Length / Config.SortedBlockSize;
						mdBlock = ReadBlock (LocalThreadAllocatedBlock (), numBlocks - 1);
						byte[] blockCopy = (byte[])mdBlock.Clone ();
						_cache.SetBlock (_baseFileName, _level, _version, int.MaxValue, blockCopy);
					}
				} else {
					numBlocks = (int)internalFileStream.Length / Config.SortedBlockSize;
					mdBlock = ReadBlock (LocalThreadAllocatedBlock (), numBlocks - 1);
				}

				MemoryStream ms = new MemoryStream (mdBlock);
				BinaryReader reader = new BinaryReader (ms);
				string checkString = Encoding.ASCII.GetString (reader.ReadBytes (8));
				if (checkString != "@RAZORDB")
					throw new InvalidDataException ("This does not appear to be a valid table file.");
				_totalBlocks = reader.Read7BitEncodedInt ();
				_dataBlocks = reader.Read7BitEncodedInt ();
				_indexBlocks = reader.Read7BitEncodedInt ();

				if (_totalBlocks != numBlocks && numBlocks != -1)
					throw new InvalidDataException ("The file size does not match the metadata size.");
				if (_totalBlocks != (_dataBlocks + _indexBlocks + 1))
					throw new InvalidDataException ("Corrupted metadata.");
			} catch (Exception ex) {
				if (Config.ExceptionHandling == ExceptionHandling.ThrowAll)
					throw;

				HandleEmptySortedBlockTable (ex);
			}
		}

		void HandleEmptySortedBlockTable (Exception ex)
		{
			_totalBlocks = 0;
			_dataBlocks = 0;
			_indexBlocks = 0;
			FileExists = false;
			Config.LogError ("ReadMetadata {0}\nException: {1}", _path, ex.Message);
		}

		public static bool Lookup (string baseFileName, int level, int version, RazorCache cache, Key key, out Value value, ExceptionHandling exceptionHandling, Action<string> logger)
		{
			SortedBlockTable sbt = new SortedBlockTable (cache, baseFileName, level, version);
			try {
				int dataBlockNum = FindBlockForKey (baseFileName, level, version, cache, key);
				if (dataBlockNum >= 0 && dataBlockNum < sbt._dataBlocks) {
					byte[] block = sbt.ReadBlock (LocalThreadAllocatedBlock (), dataBlockNum);
					return SearchBlockForKey (block, key, out value);
				}
			} finally {
				sbt.Close ();
			}
			value = Value.Empty;
			return false;
		}

		static int FindBlockForKey (string baseFileName, int level, int version, RazorCache indexCache, Key key)
		{
			Key[] index = indexCache.GetBlockTableIndex (baseFileName, level, version);
			int dataBlockNum = Array.BinarySearch (index, key);
			if (dataBlockNum < 0)
				dataBlockNum = ~dataBlockNum - 1;
			return dataBlockNum;
		}

		public IEnumerable<KeyValuePair<Key, Value>> Enumerate ()
		{
			return EnumerateFromKey (_cache, Key.Empty);
		}

		public IEnumerable<KeyValuePair<Key, Value>> EnumerateFromKey (RazorCache indexCache, Key key)
		{
			if (!FileExists)
				yield break;

			int startingBlock;
			if (key.Length == 0)
				startingBlock = 0;
			else {
				startingBlock = FindBlockForKey (_baseFileName, _level, _version, indexCache, key);
				if (startingBlock < 0)
					startingBlock = 0;
			}

			if (startingBlock < _dataBlocks) {
				byte[] allocBlockA = new byte[Config.SortedBlockSize];
				byte[] allocBlockB = new byte[Config.SortedBlockSize];
				byte[] currentBlock = allocBlockA;

				var asyncResult = BeginReadBlock (currentBlock, startingBlock);

				try {

					for (int i = startingBlock; i < _dataBlocks; i++) {

						// wait on last block read to complete so we can start processing the data
						byte[] block = EndReadBlock (asyncResult);
						asyncResult = null;

						// Go ahead and kick off the next block read asynchronously while we parse the last one
						if (i < _dataBlocks) {
							SwapBlocks (allocBlockA, allocBlockB, ref currentBlock); // swap the blocks so we can issue another disk i/o
							asyncResult = BeginReadBlock (currentBlock, i + 1);
						}

						int offset = 2; // reset offset, start after tree root pointer

						// On the first block, we need to seek to the key first (if we don't have an empty key)
						if (i == startingBlock && key.Length != 0) {
							while (offset >= 0) {
								var pair = ReadPair (block, ref offset);
								if (pair.Key.CompareTo (key) >= 0) {
									yield return pair;
									break;
								}
							}
						}

						while (offset >= 0)
							yield return ReadPair (block, ref offset); // loop through the rest of the block
					}

				} finally {
					if (asyncResult != null)
						EndReadBlock (asyncResult);
				}
			}
		}

		IEnumerable<Key> EnumerateIndex ()
		{
			if (!FileExists)
				yield break;

			byte[] allocBlockA = new byte[Config.SortedBlockSize];
			byte[] allocBlockB = new byte[Config.SortedBlockSize];
			byte[] currentBlock = allocBlockA;

			var endIndexBlocks = (_dataBlocks + _indexBlocks);
			var asyncResult = BeginReadBlock (currentBlock, _dataBlocks);

			try {
				for (int i = _dataBlocks; i < endIndexBlocks; i++) {

					// wait on last block read to complete so we can start processing the data
					byte[] block = EndReadBlock (asyncResult);
					asyncResult = null;

					// Go ahead and kick off the next block read asynchronously while we parse the last one
					if (i < endIndexBlocks) {
						SwapBlocks (allocBlockA, allocBlockB, ref currentBlock); // swap the blocks so we can issue another disk i/o
						asyncResult = BeginReadBlock (currentBlock, i + 1);
					}

					int offset = 0;
					while (offset >= 0)
						yield return ReadKey (block, ref offset);
				}
			} finally {
				if (asyncResult != null)
					EndReadBlock (asyncResult);
			}
		}

		public Key[] GetIndex ()
		{
			return EnumerateIndex ().ToArray ();
		}

		static Key ReadKey (byte[] block, ref int offset)
		{
			int keySize = Helper.Decode7BitInt (block, ref offset);
			var key = ByteArray.From (block, offset, keySize);
			offset += keySize;
			if (block [offset] == 0)
				offset = -1; // if the next keySize bit is zero then we have exhausted this block. Set to -1 to terminate enumeration

			return new Key (key);
		}

		static bool ScanBlockForKey (byte[] block, Key key, out Value value)
		{
			int offset = 2; // skip over the tree root pointer
			value = Value.Empty;

			while (offset >= 2 && offset < Config.SortedBlockSize && block[offset] == (byte)RecordHeaderFlag.Record) {
				int startingOffset = offset;
				offset++; // skip past the header flag
				offset += 4; // skip past the tree pointers
				int keySize = Helper.Decode7BitInt (block, ref offset);
				int cmp = key.CompareTo (block, offset, keySize);
				if (cmp == 0) {
					// Found it
					var pair = ReadPair (block, ref startingOffset);
					value = pair.Value;
					return true;
				} else if (cmp < 0)
					return false;
				offset += keySize;
				// Skip past the value
				int valueSize = Helper.Decode7BitInt (block, ref offset);
				offset += valueSize;
			}
			return false;
		}

		static bool SearchBlockForKey (byte[] block, Key key, out Value value)
		{
			int offset = BitConverter.ToUInt16 (block, 0); // grab the tree root
			value = Value.Empty;

			while (offset >= 2 && offset < Config.SortedBlockSize && block[offset] == (byte)RecordHeaderFlag.Record) {
				int startingOffset = offset;
				offset += 1; // skip header
				offset += 4; // skip tree pointers
				int keySize = Helper.Decode7BitInt (block, ref offset);
				int cmp = key.CompareTo (block, offset, keySize);
				if (cmp == 0) {
					// Found it
					var pair = ReadPair (block, ref startingOffset);
					value = pair.Value;
					return true;
				} else if (cmp < 0)
					offset = BitConverter.ToUInt16 (block, startingOffset + 1); // key < node => explore left side
				else if (cmp > 0)
					offset = BitConverter.ToUInt16 (block, startingOffset + 3); // key > node => explore right side
			}
			return false;
		}

		static KeyValuePair<Key, Value> ReadPair (byte[] block, ref int offset)
		{
			offset += 1; // skip over header flag
			offset += 4; // skip over the tree pointers
			int keySize = Helper.Decode7BitInt (block, ref offset);
			var key = new Key (ByteArray.From (block, offset, keySize));
			offset += keySize;
			int valueSize = Helper.Decode7BitInt (block, ref offset);
			var val = Value.From (block, offset, valueSize);
			offset += valueSize;

			// if the next keySize bit is zero then we have exhausted this block. Set to -1 to terminate enumeration
			if (offset >= Config.SortedBlockSize || block [offset] == (byte)RecordHeaderFlag.EndOfBlock)
				offset = -1;

			return new KeyValuePair<Key, Value> (key, val);
		}

		public static IEnumerable<KeyValuePair<Key, Value>> EnumerateMergedTablesPreCached (RazorCache cache, string baseFileName, IEnumerable<PageRef> tableSpecs, ExceptionHandling exceptionHandling, Action<string> logger)
		{
			var tables = tableSpecs.Select (pageRef => new SortedBlockTable (cache, baseFileName, pageRef.Level, pageRef.Version)).ToList ();
			try {
				foreach (var pair in MergeEnumerator.Merge(tables.Select(t => t.Enumerate().ToList().AsEnumerable()), t => t.Key))
					yield return pair;
			} finally {
				tables.ForEach (t => t.Close ());
			}
		}

		public static IEnumerable<PageRecord> MergeTables (RazorCache cache, Manifest mf, int destinationLevel, IEnumerable<PageRef> tableSpecs, ExceptionHandling exceptionHandling, Action<string> logger)
		{
			var orderedTableSpecs = tableSpecs.OrderByPagePriority ();
			var outputTables = new List<PageRecord> ();
			SortedBlockTableWriter writer = null;

			Key firstKey = new Key ();
			Key lastKey = new Key ();
			Key maxKey = new Key (); // Maximum key we can span with this table to avoid covering more than 10 pages in the destination

			Action<KeyValuePair<Key, Value>> OpenPage = (pair) => {
				writer = new SortedBlockTableWriter (mf.BaseFileName, destinationLevel, mf.NextVersion (destinationLevel));
				firstKey = pair.Key;
				using (var m = mf.GetLatestManifest())
					maxKey = m.FindSpanningLimit (destinationLevel + 1, firstKey);
			};

			Action ClosePage = () => {
				writer.Close ();
				outputTables.Add (new PageRecord (destinationLevel, writer.Version, firstKey, lastKey));
				writer = null;
			};

			foreach (var pair in EnumerateMergedTablesPreCached(cache, mf.BaseFileName, orderedTableSpecs, exceptionHandling, logger)) {
				if (writer == null)
					OpenPage (pair);
				if (writer.WrittenSize >= Config.MaxSortedBlockTableSize || (!maxKey.IsEmpty && pair.Key.CompareTo (maxKey) >= 0))
					ClosePage ();
				writer.WritePair (pair.Key, pair.Value);
				lastKey = pair.Key;
			}
			if (writer != null)
				ClosePage ();

			return outputTables;
		}

		string BytesToString (byte[] block, int start, int length)
		{
			return string.Concat (block.Skip (start).Take (length).Select ((b) => b.ToString ("X2")).ToArray ());
		}

		public void DumpContents (Action<string> msg)
		{
			msg (string.Format ("Path: {0}", _path));
			msg (string.Format ("BaseFileName: {0} Level: {1} Version: {2}", _baseFileName, _level, _version));
			msg (string.Format ("Data Blocks: {0}\nIndex Blocks: {1}\nTotal Blocks: {2}", _dataBlocks, _indexBlocks, _totalBlocks));
			msg ("");

			for (int i = 0; i < _dataBlocks; i++) {
				msg (string.Format ("\n*** Data Block {0} ***", i));
				byte[] block = ReadBlock (new byte[Config.SortedBlockSize], i);

				int treePtr = BitConverter.ToUInt16 (block, 0);
				msg (string.Format ("{0:X4} \"{1}\" Tree Offset: {2:X4}", 0, BytesToString (block, 0, 2), treePtr));

				int offset = 2;
				while (offset < Config.SortedBlockSize && block[offset] != (byte)RecordHeaderFlag.EndOfBlock) {

					// Record
					msg (string.Format ("{0:X4} \"{1}\" {2}", offset, BytesToString (block, offset, 1), ((RecordHeaderFlag)block [offset]).ToString ()));

					// Node Pointers
					msg (string.Format ("{0:X4} \"{1}\" Left:  {2:X4}", offset + 1, BytesToString (block, offset + 1, 2), BitConverter.ToUInt16 (block, offset + 1)));
					msg (string.Format ("{0:X4} \"{1}\" Right: {2:X4}", offset + 3, BytesToString (block, offset + 3, 2), BitConverter.ToUInt16 (block, offset + 3)));
					offset += 5;

					// Key
					int keyOffset = offset;
					int keySize = Helper.Decode7BitInt (block, ref offset);
					msg (string.Format ("{0:X4} \"{1}\" KeySize: {2}", keyOffset, BytesToString (block, keyOffset, offset - keyOffset), keySize));
					msg (string.Format ("{0:X4} \"{1}\"", offset, BytesToString (block, offset, keySize)));
					offset += keySize;

					// Key
					int dataOffset = offset;
					int dataSize = Helper.Decode7BitInt (block, ref offset);
					msg (string.Format ("{0:X4} \"{1}\" DataSize: {2}", dataOffset, BytesToString (block, dataOffset, offset - dataOffset), dataSize));
					msg (string.Format ("{0:X4} \"{1}\"", offset, BytesToString (block, offset, dataSize)));
					offset += dataSize;
				}
			}
		}

		public void ScanCheck ()
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
				foreach (var pair in Enumerate()) {
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
					} catch (Exception ex) {
						Config.LogError ("Error Reading Record: {0}", ex);
					}
				}
			} catch (Exception ex) {
				Config.LogError ("Error Enumerating File: {0}", ex);
			} finally {
				Console.WriteLine ("  KeyBytes: {0}, ValueBytes: {1}\n  Records: {2} Deleted: {3} Null: {4} Small: {5} LargeDesc: {6} LargeChunk: {7}", totalKeyBytes, totalValueBytes, totalRecords, deletedRecords, nullRecords, smallRecords, largeDescRecords, largeChunkRecords);
			}
		}

		public void Close ()
		{
			if (_fileStream != null)
				_fileStream.Close ();
			_fileStream = null;
		}
	}
}