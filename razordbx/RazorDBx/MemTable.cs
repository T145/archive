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

namespace RazorDBx
{
	public class MemTable
	{
		RazorDBx.C5.TreeDictionary<Key, Value> _internalTable = new RazorDBx.C5.TreeDictionary<Key, Value> ();
		int _totalKeySize = 0;
		int _totalValueSize = 0;
		object _tableLock = new object ();

		public void Add (Key key, Value value) // Add the specified key and value.
		{
			lock (_tableLock) {
				_totalKeySize += key.Length;
				_totalValueSize += value.Length;
				_internalTable [key] = value; // Set value in the hashtable
			}
		}

		public bool Lookup (Key key, out Value value) // Lookup the specified key and value.
		{
			lock (_tableLock)
				return _internalTable.Find (ref key, out value);
		}

		public int Size { // Gets the size.
			get {
				lock (_tableLock)
					return _totalKeySize + _totalValueSize;
			}
		}

		public bool Full { // Gets a value indicating if this memtable is full.
			get {
				return Size > Config.MaxMemTableSize;
			}
		}

		public Key FirstKey { // Gets the first key.
			get {
				lock (_tableLock)
					return _internalTable.FindMin ().Key;
			}
		}

		public Key LastKey { // Gets the last key.
			get {
				lock (_tableLock)
					return _internalTable.FindMax ().Key;
			}
		}

		public void WriteToSortedBlockTable (string baseFileName, int level, int version) // Writes to the sorted block table.
		{
			lock (_tableLock) {
				SortedBlockTableWriter tableWriter = null;
				try {
					tableWriter = new SortedBlockTableWriter (baseFileName, level, version);

					foreach (var pair in this.Enumerate())
						tableWriter.WritePair (pair.Key, pair.Value);
				} finally {
					if (tableWriter != null)
						tableWriter.Close ();
				}
			}
		}

		public IEnumerable<KeyValuePair<Key, Value>> Enumerate () // Enumerates the current instance.
		{
			return _internalTable.Select (pair => new KeyValuePair<Key, Value> (pair.Key, pair.Value));
		}

		public IEnumerable<KeyValuePair<Key, Value>> GetEnumerableSnapshot () // Gets the enumerable snapshot.
		{
			lock (_tableLock)
				return _internalTable.Snapshot ().Select (pair => new KeyValuePair<Key, Value> (pair.Key, pair.Value));
		}

		public void ReadFromJournal (string fileName, int version) // Reads from the journal.
		{
			lock (_tableLock) {
				JournalReader jr = new JournalReader (fileName, version);
				try {
					foreach (var pair in jr.Enumerate())
						Add (pair.Key, pair.Value);
				} finally {
					jr.Close ();
				}
			}
		}
	}
}