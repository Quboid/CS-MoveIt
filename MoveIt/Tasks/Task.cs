using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MoveIt.Tasks
{
    internal class Task
    {
        /// <summary>
        /// The Action object that called this task
        /// </summary>
        internal Action CallingAction { get => _callingAction; set => _callingAction = value; }
        private Action _callingAction;

        /// <summary>
        /// The thread to execute the codeblock on
        /// </summary>
        internal Threads Thread { get => _thread; set => _thread = value; }
        private Threads _thread;

        /// <summary>
        /// The task's status
        /// </summary>
        internal Statuses Status { get => _status; set => _status = value; }
        private Statuses _status;

        /// <summary>
        /// The code to execute in Thread
        /// </summary>
        internal DCodeBlock CodeBlock { get => _codeBlock; set => _codeBlock = value; }
        private DCodeBlock _codeBlock;
        internal delegate void DCodeBlock();

        internal Task(Action action, Threads thread, DCodeBlock codeBlock)
        {
            CodeBlock = codeBlock;
            CallingAction = action;
            Thread = thread;
            Status = Statuses.Waiting;
        }

        internal bool Execute()
        {
            try
            {
                Status = Statuses.Processing;

                switch (Thread)
                {
                    case Threads.Simulation:
                        Singleton<SimulationManager>.instance.AddAction(() =>
                        {
                            try
                            {
                                CodeBlock();
                                this.Finish();
                            }
                            catch (Exception e)
                            {
                                Log.Error(e);
                            }
                        });
                        break;

                    case Threads.Main:
                        SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(() =>
                        {
                            try
                            {
                                CodeBlock();
                                this.Finish();
                            }
                            catch (Exception e)
                            {
                                Log.Error(e);
                            }
                        });
                        break;

                    default:
                        Log.Error($"Task called invalid thread!", "[MI77");
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                return false;
            }
            return true;
        }

        internal void Finish()
        {
            Status = Statuses.Finished;
        }

        internal static Task Create(Action action, Threads thread, DCodeBlock codeBlock)
        {
            Task t = new Task(action, thread, codeBlock);

            return t;
        }
    }

    internal enum Statuses
    {
        None,
        Waiting,
        Processing,
        Finished
    }

    internal enum Threads
    {
        None,
        Main,
        Simulation
    }
}
