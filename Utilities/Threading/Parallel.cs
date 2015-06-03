using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Utilities
{
    public delegate void ForDelegate(int i);
    public delegate void DelegateProcess();

    // Based on:
    // http://coding-time.blogspot.pt/2008/03/implement-your-own-parallelfor-in-c.html
    // C# 2.0 adaptation based on:
    // http://dotnetgalactics.wordpress.com/2009/11/19/how-to-provide-a-parallel-for-loop-in-c2-0-2/
    public class Parallel
    {
        /// <summary>
        /// Parallel for loop. Invokes given action, passing arguments
        /// fromInclusive - toExclusive on multiple threads.
        /// Returns when loop finished.
        /// </summary>
        public static void For(int fromInclusive, int toExclusive, ForDelegate forDelegate)
        {
            // chunkSize = 1 makes items to be processed in order.
            // Bigger chunk size should reduce lock waiting time and thus
            // increase paralelism.
            int chunkSize = 4;

            // number of process() threads
            int threadCount = Environment.ProcessorCount;
            int index = fromInclusive - chunkSize;
            // locker object shared by all the process() delegates
            object locker = new object();

            // processing function
            // takes next chunk and processes it using action
            DelegateProcess process = delegate()
            {
                while (true)
                {
                    int chunkStart = 0;
                    lock (locker)
                    {
                        // take next chunk
                        index += chunkSize;
                        chunkStart = index;
                    }
                    // process the chunk
                    // (another thread is processing another chunk 
                    //  so the real order of items will be out-of-order)
                    for (int i = chunkStart; i < chunkStart + chunkSize; i++)
                    {
                        if (i >= toExclusive) return;
                        forDelegate(i);
                    }
                }
            };

            // launch process() threads
            IAsyncResult[] asyncResults = new IAsyncResult[threadCount];
            for (int i = 0; i < threadCount; ++i)
            {
                asyncResults[i] = process.BeginInvoke(null, null);
            }
            // wait for all threads to complete
            for (int i = 0; i < threadCount; ++i)
            {
                process.EndInvoke(asyncResults[i]);
            }
        }
    }
}