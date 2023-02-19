using System.Collections.Generic;

namespace MoveIt.Tasks
{
    internal class TaskManagement
    {
        internal Queue<Batch> Batches;
        internal Batch Current;

        internal TaskManagement()
        {
            Batches = new Queue<Batch>();
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
        }

        internal void AddBatch(Batch batch)
        {
            if (Batches == null) Batches = new Queue<Batch>();

            Batches.Enqueue(batch);
        }
    }
}
