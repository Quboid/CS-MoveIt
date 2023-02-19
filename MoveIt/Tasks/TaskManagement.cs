using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MoveIt.Tasks
{
    internal class TaskManagement
    {
        internal Queue<Batch> Batches;
        internal Batch Current;
        internal List<Task> Executing;

        internal bool IsProcessing => (Executing.Count == 0);

        internal TaskManagement()
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

            // Check if current batch is complete
            if (Current != null)
            {
                if (Current.Status == Batch.Statuses.Finished)
                {
                    Batches.Dequeue();
                    Current = null;
                }
            }

            // If no current batch is loaded, grab the next one
            if (Current == null)
            {
                if (Batches.Count > 0)
                { // Get next batch
                    Current = Batches.Peek();
                }
                else
                { // Nothing to do
                    return;
                }
            }

            Current.Update();

            //// Check for completed tasks, remove them from the execution list
            //if (Executing.Count > 0)
            //{
            //    List<Task> executingBuffer = new List<Task>(Executing);
            //    foreach (Task t in executingBuffer)
            //    {
            //        if (t.Status == Task.Statuses.Finished)
            //        {
            //            Executing.Remove(t);
            //        }
            //    }
            //}

            //// If not all executing tasks have finished, complete update cycle
            //if (Executing.Count > 0)
            //{
            //    return;
            //}

            //if (Current != null)
            //{
            //    // See if any tasks in the batch need to start
            //    foreach (Task t in Current.Tasks)
            //    {
            //        if (t.Status == Task.Statuses.Waiting)
            //        {
            //            t.Execute();
            //            Executing.Add(t);
            //        }
            //    }

            //    // Has this batch finished?
            //    if (Current.IsFinished())
            //    {
            //        Batches.Dequeue();
            //        Current = null;
            //    }
            //}

            //// If no current batch is loaded, grab the next one
            //if (Current == null)
            //{
            //    if (Batches.Count > 0)
            //    {
            //        Current = Batches.Peek();
            //    }
            //}
        }

        internal void AddBatch(Batch batch)
        {
            if (Batches == null) Batches = new Queue<Batch>();

            Batches.Enqueue(batch);
        }
    }
}
