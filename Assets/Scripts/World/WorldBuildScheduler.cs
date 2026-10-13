using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace PoeClone.World
{
    /// <summary>
    /// Runs construction checkpoints until the frame's work budget is spent. A null in a
    /// build routine is a pacing checkpoint; real Unity wait instructions are preserved.
    /// Synchronization waits belong outside this scheduler.
    /// </summary>
    public static class WorldBuildScheduler
    {
        public static IEnumerator Run(IEnumerator routine, double milliseconds)
        {
            if (milliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            var clock = Stopwatch.StartNew();
            try
            {
                while (stack.Count > 0)
                {
                    IEnumerator step = stack.Peek();
                    if (!step.MoveNext())
                    {
                        stack.Pop();
                        (step as IDisposable)?.Dispose();
                    }
                    else if (step.Current is UnityEngine.YieldInstruction || step.Current is UnityEngine.CustomYieldInstruction)
                    {
                        // CustomYieldInstruction also implements IEnumerator. Unity, rather
                        // than the construction scheduler, must advance those actual waits.
                        yield return step.Current;
                        clock.Restart();
                    }
                    else if (step.Current is IEnumerator nested)
                        stack.Push(nested);
                    else if (step.Current != null)
                    {
                        yield return step.Current;
                        clock.Restart();
                    }
                    if (stack.Count > 0 && clock.Elapsed.TotalMilliseconds >= milliseconds)
                    {
                        yield return null;
                        clock.Restart();
                    }
                }
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            }
        }
    }
}
