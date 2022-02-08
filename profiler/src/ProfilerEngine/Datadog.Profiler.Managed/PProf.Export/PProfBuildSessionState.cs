// <copyright file="PProfBuildSessionState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.PProf.Export
{
    internal class PProfBuildSessionState
    {
        // The session lists logically belong to a session instance.
        // However, we only have one concurrent session at a time and those lists can be long, so that the underlying objects can get into the LOH.
        // So we keep them here and clear them after each session.

        private readonly object _sessionLock = new object();
        private readonly List<PProfInfo.Location> _locations = new List<PProfInfo.Location>();
        private readonly List<PProfInfo.Mapping> _mappings = new List<PProfInfo.Mapping>();
        private readonly List<PProfInfo.Function> _functions = new List<PProfInfo.Function>();
        private readonly List<PProfInfo.String> _stringTable = new List<PProfInfo.String>();
        private PProfBuildSession _currentSession = null;

        public object SessionLock
        {
            get { return _sessionLock; }
        }

        public PProfBuildSession Session
        {
            get { return _currentSession; } set { _currentSession = value; }
        }

        public List<PProfInfo.Location> Locations
        {
            get { return _locations; }
        }

        public List<PProfInfo.Mapping> Mappings
        {
            get { return _mappings; }
        }

        public List<PProfInfo.Function> Functions
        {
            get { return _functions; }
        }

        public List<PProfInfo.String> StringTable
        {
            get { return _stringTable; }
        }

        public bool HasSesionListItems
        {
            get
            {
                return (_locations.Count > 0) || (_mappings.Count > 0) || (_functions.Count > 0) || (_stringTable.Count > 0);
            }
        }

        public void ResetSession()
        {
            // Clear Locations:
            {
                var list = _locations;
                int itemCount = list.Count;
                for (int i = 0; i < itemCount; i++)
                {
                    PProfInfo.Location pprofInfo = list[i];
                    pprofInfo.IsIncludedInSession = false;
                }

                list.Clear();
            }

            // Clear Mappings:
            {
                var list = _mappings;
                int itemCount = list.Count;
                for (int i = 0; i < itemCount; i++)
                {
                    PProfInfo.Mapping pprofInfo = list[i];
                    pprofInfo.IsIncludedInSession = false;
                    pprofInfo.Item.Filename = ProtoConstants.StringTableIndex.GetUnresolvedIfSet(pprofInfo.Item.Filename);
                    pprofInfo.Item.BuildId = ProtoConstants.StringTableIndex.GetUnresolvedIfSet(pprofInfo.Item.BuildId);
                }

                list.Clear();
            }

            // Clear Functions:
            {
                var list = _functions;
                int itemCount = list.Count;
                for (int i = 0; i < itemCount; i++)
                {
                    PProfInfo.Function pprofInfo = list[i];
                    pprofInfo.IsIncludedInSession = false;
                    pprofInfo.Item.Name = ProtoConstants.StringTableIndex.GetUnresolvedIfSet(pprofInfo.Item.Name);
                    pprofInfo.Item.SystemName = ProtoConstants.StringTableIndex.GetUnresolvedIfSet(pprofInfo.Item.SystemName);
                    pprofInfo.Item.Filename = ProtoConstants.StringTableIndex.GetUnresolvedIfSet(pprofInfo.Item.Filename);
                }

                list.Clear();
            }

            // Clear the String Table:
            {
                var list = _stringTable;
                int itemCount = list.Count;
                for (int i = 0; i < itemCount; i++)
                {
                    list[i].ResetOffsetInStringTable();
                }

                list.Clear();
            }

            // Finally, clear the Session:
            _currentSession = null;
        }
    }
}