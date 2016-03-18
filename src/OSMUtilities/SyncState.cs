// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace ESRI.ArcGIS.OSM.OSMUtilities
{
    /// <summary>Store and Retrieve sync state of a local OSM dataset</summary>
    /// <remarks>
    /// Currently, the OSM Diff Loader is quite slow, so it only makes sense to sync a downloaded dataset
    /// if it's less than 3 hours old. If it's older than this, it's fastest to redownload the whole thing.
    /// Eventually, this sync limit can be increased when we figure out how to make the diff download faster.
    /// </remarks>
    /// 
    [ComVisible(false)]
    public static class SyncState
    {
        private const string _SYNC_STATE_DIR = @"\ESRI\OSMEditor";
        private const string _SYNC_STATE_FILE = "SyncState.txt";
        private const char _SYNC_STATE_DELIM = '|';

        private const int _MAX_SYNC_HOURS = 3;

        /// <summary>Retrieve the path of the SyncState file</summary>
        public static string SyncStateFile()
        {
            string appFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(appFolder + _SYNC_STATE_DIR, _SYNC_STATE_FILE);
        }

        /// <summary>Checks the SyncState.txt file to see if the given dataset can be synced or needs a full download</summary>
        /// <remarks>
        /// A full download is required if:
        /// - the dataset is not in the syncState.txt file
        /// - the last sync time of the dataset is older than _MAX_SYNC_HOURS hours
        /// </remarks>
        public static bool CanSyncDataset(string datasetName)
        {
            DateTime lastSyncTime = RetrieveLastSyncTime(datasetName);
            return (DateTime.Now.Subtract(lastSyncTime).TotalHours <= _MAX_SYNC_HOURS);
        }

        /// <summary>Reads the last sync time from the SyncStateFile</summary>
        public static DateTime RetrieveLastSyncTime(string datasetName)
        {
            DateTime syncTime = DateTime.Now.AddDays(-30);

            string syncStateFile = SyncStateFile();
            if (!File.Exists(syncStateFile))
                return syncTime;

            string syncLine = EnumerateFileLines(syncStateFile)
                .FirstOrDefault(ln => LineContainsDatasetName(ln, datasetName));

            if (!string.IsNullOrEmpty(syncLine))
            {
                string[] parts = syncLine.Split(_SYNC_STATE_DELIM);
                if (parts.Length == 2)
                    DateTime.TryParse(parts[1], out syncTime);
            }

            return syncTime;
        }

        /// <summary>Writes the last sync time to the SyncStateFile</summary>
        public static void StoreLastSyncTime(string datasetName, DateTime syncTime)
        {
            string syncStateFile = SyncStateFile();

            StringBuilder stringBuilder = new StringBuilder();
            bool fileUpdateInfoFound = false;

            if (File.Exists(syncStateFile))
            {
                foreach (string line in EnumerateFileLines(syncStateFile))
                {
                    if (LineContainsDatasetName(line, datasetName))
                    {
                        stringBuilder.AppendLine(datasetName + _SYNC_STATE_DELIM + syncTime.ToUniversalTime().ToString("u"));
                        fileUpdateInfoFound = true;
                    }
                    else
                    {
                        stringBuilder.AppendLine(line);
                    }
                }
            }

            if (!fileUpdateInfoFound)
                stringBuilder.AppendLine(datasetName + _SYNC_STATE_DELIM + syncTime.ToUniversalTime().ToString("u"));

            using (StreamWriter streamWriter = CreateTextFile(syncStateFile))
            {
                streamWriter.Write(stringBuilder.ToString());
            }
        }

        /// <summary>Creates a text file (and directory structure if it does not exist)</summary>
        private static StreamWriter CreateTextFile(string filePath)
        {
            string folder = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return File.CreateText(filePath);
        }

        /// <summary>Enumerate the lines of a text file</summary>
        private static IEnumerable<string> EnumerateFileLines(string fileName)
        {
            if (!File.Exists(fileName))
                yield break;

            using (StreamReader streamReader = new StreamReader(fileName))
            {
                string line = null;
                while ((line = streamReader.ReadLine()) != null)
                    yield return line;
            }
        }

        /// <summary>Returns whether the current line contains the dataset (compares base dataset name only)</summary>
        private static bool LineContainsDatasetName(string line, string datasetName)
        {
            string[] parts = line.Split(_SYNC_STATE_DELIM);
            if (parts.Length != 2)
                return false;

            string cnxName = Path.GetDirectoryName(datasetName);

            string baseDatasetName = Path.GetFileName(datasetName);
            int dotIdx = baseDatasetName.LastIndexOf('.');
            if (dotIdx >= 0)
                baseDatasetName = baseDatasetName.Substring(dotIdx + 1);

            return (parts[0].StartsWith(cnxName) && parts[0].EndsWith(baseDatasetName));
        }
    }
}
