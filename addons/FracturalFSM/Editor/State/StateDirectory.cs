
using System;
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Fractural.StateMachine
{
    /// <summary>
    /// Helper class instance for traversing state machine directories (nested states).
    /// 
    /// ie. root --> someState --> someOtherState --> endState
    /// </summary>
    public struct StateDirectory
    {
        public string Path { get; set; }
        /// <summary>
        /// Get current full path
        /// </summary>
        public string CurrentPath
        {
            get
            {
                string result = "";
                for (int i = 0; i < currentIndex; i++)
                    result += dirs[i];
                return result;
            }
        }
        /// <summary>
        /// Get base state name
        /// </summary>
        public string Base => dirs[BaseIndex];
        /// <summary>
        /// Get end state name
        /// </summary>
        public string End => dirs[EndIndex];

        private string[] dirs; // Empty string equals to root
        /// <summary>
        /// Get arrays of directories
        /// </summary>
        /// <returns></returns>
        public string[] Dirs => dirs.Clone() as string[];

        private int currentIndex;

        public StateDirectory(string p)
        {
            Path = p;
            List<string> dirsList = new List<string>() { "" };  // Empty string represents root
            dirsList.AddRange(p.Split("/"));
            dirs = dirsList.ToArray();
            currentIndex = 0;
        }

        /// <summary>
        /// Move to next level && return state if exists, else null
        /// </summary>
        /// <returns></returns>
        public string GotoNext()
        {
            if (HasNext)
            {
                currentIndex += 1;
                return CurrentEnd;
            }
            return null;

        }

        /// <summary>
        /// Move to previous level && return state if exists, else null
        /// </summary>
        /// <returns></returns>
        public string GotoPrevious()
        {
            if (HasPrevious)
            {
                currentIndex -= 1;
                return CurrentEnd;
            }
            return null;
        }

        /// <summary>
        /// Move to specified index && return state
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public string Goto(int index)
        {
            System.Diagnostics.Debug.Assert(index > -1 && index < dirs.Length);
            currentIndex = index;
            return CurrentEnd;
        }

        /// <summary>
        /// Check if directory has next level
        /// </summary>
        public bool HasNext => currentIndex < dirs.Length - 1;

        /// <summary>
        /// Check if directory has previous level
        /// </summary>
        public bool HasPrevious => currentIndex > 0;

        /// <summary>
        /// Get current end state name of path
        /// </summary>
        public string CurrentEnd => StateDirectory.GetStateFromPath(CurrentPath);

        /// <summary>
        /// Get index of base state
        /// </summary>
        public int BaseIndex => 1; // Root(empty string) at index 0, base at index 1

        /// <summary>
        /// Get level index of end state
        /// </summary>
        public int EndIndex => dirs.Length - 1;

        /// <summary>
        /// Check if it is Entry state
        /// </summary>
        public bool IsEntry => End == State.EntryState;

        /// <summary>
        /// Check if it is Exit state
        /// </summary>
        public bool IsExit => End == State.ExitState;

        /// <summary>
        /// Check if it is nested. ("Base" is not nested, "Base/NextState" is nested)
        /// </summary>
        public bool IsNested => dirs.Length > 2; // Root(empty string) & base taken 2 place

        /// <summary>
        /// Return parent path, "path/to/state" return "path/to"
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetBaseDirectoryFromPath(string path)
        {
            int lastSlashIdx = path.FindLast("/");
            if (lastSlashIdx < 0)
                return "";
            return path.Substr(0, lastSlashIdx);
        }

        /// <summary>
        /// Return end directory of path, "path/to/state" returns "state"
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetStateFromPath(string path)
        {
            int lastSlashIdx = path.FindLast("/");
            if (lastSlashIdx < 0)
                return path;
            return path.Substring(lastSlashIdx + 1);
        }
    }
}