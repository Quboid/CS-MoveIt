using System;
using System.Collections.Generic;

namespace MoveIt.Tasks
{
    internal class Batch
    {
        internal List<Task> Tasks;
        internal int Size => Tasks.Count;
        internal string Name;

        internal Task Prefix = null;
        internal Task Postfix = null;

        /// <summary>
        /// The batch's status
        /// </summary>
        internal Statuses Status { get => _status; set => _status = value; }
        private Statuses _status;

        internal Batch(List<Task> tasks, Task prefix, Task postfix, string name)
        {
            Tasks = tasks;
            Prefix = prefix;
            Postfix = postfix;

            Status = Statuses.Start;
            Name = name;
        }

        internal void Update()
        {
            try
            {
                Log.Debug($"AAA01 [{Name}] Tasks:{Tasks.Count}, Status:{Status}");

                switch (Status)
                {
                    case Statuses.Start:
                        if (Prefix != null)
                        {
                            Prefix.Execute();
                            Status = Statuses.Prefix;
                        }
                        else
                        {
                            Status = Statuses.Waiting;
                        }
                        break;

                    case Statuses.Prefix:
                        if (Prefix.Status == Task.Statuses.Finished) Status = Statuses.Waiting;
                        break;

                    case Statuses.Waiting:
                        if (Tasks.Count > 0)
                        {
                            foreach (Task t in Tasks)
                            {
                                t.Execute();
                            }
                            Status = Statuses.Processing;
                        }
                        else
                        {
                            Status = Statuses.Processed;
                        }
                        break;

                    case Statuses.Processing:
                        bool complete = true;
                        foreach (Task t in Tasks)
                        {
                            if (t.Status != Task.Statuses.Finished)
                            {
                                complete = false;
                                break;
                            }
                        }
                        if (complete) Status = Statuses.Processed;
                        break;

                    case Statuses.Processed:
                        if (Postfix != null)
                        {
                            Postfix?.Execute();
                            Status = Statuses.Postfix;
                        }
                        else
                        {
                            Status = Statuses.Finished;
                        }
                        break;

                    case Statuses.Postfix:
                        if (Postfix.Status == Task.Statuses.Finished) Status = Statuses.Finished;
                        break;

                    default:
                        Log.Error($"Batch has invalid status ({Status})!");
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        internal enum Statuses
        {
            None,
            Start,
            Prefix,
            Waiting,
            Processing,
            Processed,
            Postfix,
            Finished
        }
    }
}
