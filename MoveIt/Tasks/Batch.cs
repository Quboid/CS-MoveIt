using System.Collections.Generic;

namespace MoveIt.Tasks
{
    internal class Batch
    {
        internal List<Task> Tasks;
        internal int Size => Tasks.Count;

        internal Task Prefix = null;
        internal Task Postfix = null;

        /// <summary>
        /// The batch's status
        /// </summary>
        internal Statuses Status { get => _status; set => _status = value; }
        private Statuses _status;

        internal Batch(List<Task> tasks, Task prefix, Task postfix)
        {
            Tasks = tasks;
            Prefix = prefix;
            Postfix = postfix;

            Status = Statuses.Start;
        }

        internal void Update()
        {
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
                    foreach (Task t in Tasks)
                    {
                        t.Execute();
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
