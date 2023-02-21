using System;
using System.Collections.Generic;

namespace MoveIt.QTasks
{
    internal class Batch
    {
        internal List<QTask> Tasks;
        internal int Size => Tasks.Count;
        internal string Name;

        internal QTask Prefix = null;
        internal QTask Postfix = null;

        /// <summary>
        /// The batch's status
        /// </summary>
        internal Statuses Status { get => _status; set => _status = value; }
        private Statuses _status;

        internal Batch(List<QTask> tasks, QTask prefix, QTask postfix, string name)
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
                        if (Prefix.Status == QTask.Statuses.Finished) Status = Statuses.Waiting;
                        break;

                    case Statuses.Waiting:
                        if (Tasks.Count > 0)
                        {
                            foreach (QTask t in Tasks)
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
                        foreach (QTask t in Tasks)
                        {
                            if (t.Status != QTask.Statuses.Finished)
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
                        if (Postfix.Status == QTask.Statuses.Finished) Status = Statuses.Finished;
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
