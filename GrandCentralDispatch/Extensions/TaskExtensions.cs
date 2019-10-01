using System;
using System.Threading;
using System.Threading.Tasks;

namespace GrandCentralDispatch.Extensions
{
    internal static class TaskExtensions
    {
        public static async Task WrapTaskForCancellation(this Task task, CancellationToken ct)
        {
            if (ct.CanBeCanceled && !task.IsCompleted)
            {
                var tcs = new TaskCompletionSource<bool>();
                if (ct.IsCancellationRequested)
                {
                    tcs.TrySetCanceled();
                }
                else
                {
                    ct.Register(() => { tcs.TrySetCanceled(); });
                    await task.ContinueWith(antecedent =>
                    {
                        if (antecedent.IsFaulted)
                        {
                            var exception = antecedent.Exception?.GetBaseException();
                            tcs.TrySetException(exception ??
                                                new Exception("Task is faulted but no exception has been retrieved."));
                        }
                        else if (antecedent.IsCanceled)
                        {
                            tcs.TrySetCanceled();
                        }
                        else
                        {
                            tcs.TrySetResult(true);
                        }
                    }, TaskContinuationOptions.ExecuteSynchronously);
                }

                await tcs.Task;
            }

            await task;
        }

        public static async Task<T> WrapTaskForCancellation<T>(this Task<T> task, CancellationToken ct)
        {
            if (ct.CanBeCanceled && !task.IsCompleted)
            {
                var tcs = new TaskCompletionSource<T>();
                if (ct.IsCancellationRequested)
                {
                    tcs.TrySetCanceled();
                }
                else
                {
                    ct.Register(() => { tcs.TrySetCanceled(); });
                    await task.ContinueWith(antecedent =>
                    {
                        if (antecedent.IsFaulted)
                        {
                            var exception = antecedent.Exception?.GetBaseException();
                            tcs.TrySetException(exception ??
                                                new Exception("Task is faulted but no exception has been retrieved."));
                        }
                        else if (antecedent.IsCanceled)
                        {
                            tcs.TrySetCanceled();
                        }
                        else
                        {
                            tcs.TrySetResult(antecedent.Result);
                        }
                    }, TaskContinuationOptions.ExecuteSynchronously);
                }

                return await tcs.Task;
            }

            return await task;
        }
    }
}