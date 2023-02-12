using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MoveIt.Tasks
{
    internal class TaskManager
    {
        internal Queue<Batch> Batches;
        internal Batch Current;
        internal List<Task> Executing;

        internal bool IsProcessing => (Executing.Count == 0);

        internal TaskManager()
        {
            Batches = new Queue<Batch>();
            Executing = new List<Task>();
        }

        /// <summary>
        /// Runs on each tick
        /// </summary>
        internal void Update()
        {
            if (Batches == null || Batches.Count == 0) return; // No tasks exist

            // Check for completed tasks, remove them from the execution list
            if (Executing.Count > 0)
            {
                List<Task> executingBuffer = new List<Task>(Executing);
                foreach (Task t in executingBuffer)
                {
                    if (t.Status == Statuses.Finished)
                    {
                        Executing.Remove(t);
                    }
                }
            }

            // If not all executing tasks have finished, complete update cycle
            if (Executing.Count > 0)
            {
                return;
            }

            if (Current != null)
            {
                // See if any tasks in the batch need to start
                foreach (Task t in Current.Tasks)
                {
                    if (t.Status == Statuses.Waiting)
                    {
                        t.Execute();
                        Executing.Add(t);
                    }
                }

                // Has this batch finished?
                if (Current.IsFinished())
                {
                    Batches.Dequeue();
                    Current = null;
                }
            }

            // If no current batch is loaded, grab the next one
            if (Current == null)
            {
                if (Batches.Count > 0)
                {
                    Current = Batches.Peek();
                }
            }
        }

        internal void AddBatch(Batch batch)
        {
            Batches.Enqueue(batch);
        }
    }

    internal class Batch
    {
        internal List<Task> Tasks;
        internal int Size => Tasks.Count;

        internal bool IsFinished()
        {
            foreach (Task t in Tasks)
            {
                if (t.Status != Statuses.Finished)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
