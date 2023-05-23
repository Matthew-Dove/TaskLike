using System.Runtime.CompilerServices;

namespace TaskLike
{
    file sealed class Box<T> : Alias<T> { public Box(T value) : base(value) { } } // Force T on the heap, so we can mark it volatile; and skip a lock.

    // Bag things for ResponseAsync{T}, so we can make it a readonly struct.
    file sealed class Bag<T>
    {
        public volatile Box<T> Box;
        public readonly ManualResetEventSlim Sync;
        public bool IsDisposed;
        public volatile TaskCompletionSource<Response<T>> Tcs;

        public Bag(T result)
        {
            Box = new Box<T>(result);
            Sync = null;
            IsDisposed = true;
            Tcs = null;
        }

        public Bag()
        {
            Box = null;
            Sync = new ManualResetEventSlim(false);
            IsDisposed = false;
            Tcs = null;
        }
    }

    /**
    * General flow for the task-like type ResponseAsync{T}:
    * 1) Create the custom async method builder.
    * 2) Foreach await in that method, call "OnCompleted" on said builder.
    * 3) Outside that method, get the custom awaiter (i.e. the Task); and call get result (i.e. the await keyword).
    * 4) When the method result returns (or an exception is thrown / task is canceled), inform the awaiter the task is completed; and the result is ready.
    **/
    [AsyncMethodBuilder(typeof(ResponseAsyncMethodBuilder<>))]
    public readonly struct ResponseAsync<T> : IDisposable
    {
        private readonly Bag<T> _bag;

        // Use this constructor when you already have the value to set (i.e. T is pre-calculated).
        public ResponseAsync(T result) { _bag = new Bag<T>(result); }

        public ResponseAsync() { _bag = new Bag<T>(); }

        // Static helper for the public constructor to set the result.
        public static ResponseAsync<T> FromResult(T result) => new ResponseAsync<T>(result);

        // Convert into a Task{T} type.
        public Task<Response<T>> AsTask()
        {
            if (_bag.Tcs == null)
            {
                lock (_bag.Sync) // The user calls this (from who knows where, how many times), and we only want the tcs created once.
                {
                    if (_bag.Tcs == null)
                    {
                        _bag.Tcs = new TaskCompletionSource<Response<T>>();
                        if (_bag.IsDisposed)
                        {
                            var box = _bag.Box;
                            if (box != null) _bag.Tcs.SetResult(Response.Create(box.Value)); // Task is already completed.
                            else _bag.Tcs.SetCanceled(); // There is no result, as the task generated an exception, or was canceled.

                        }
                    }
                }
            }
            return _bag.Tcs.Task;
        }

        // When true, the value is ready to be read.
        internal bool IsCompleted() { if (_bag.IsDisposed) return true; lock (_bag.Sync) { return _bag.IsDisposed; } } // Try lock free read first.

        // Called by the awaiter, after the state machine has finished, and before the result has been calculated (when using the await keyword).
        internal void Wait()
        {
            if (_bag.IsDisposed) return; // Try lock free read first.
            lock (_bag.Sync) { if (_bag.IsDisposed) return; }
            _bag.Sync.Wait();
            Dispose(disposing: true);
        }

        // Called by the awaiter when the result is requested (either manually by getting the awaiter, or when using the await keyword).
        internal Response<T> GetValue()
        {
            var box = _bag.Box;
            return box == null ? new Response<T>() : new Response<T>(box.Value); // If null, then a result was never set for this task.
        }

        // Called by the state machine when the method has returned a result.
        internal void SetValue(T value)
        {
            _bag.Box = new Box<T>(value);
            _bag.Tcs?.SetResult(Response.Create(value));
            _bag.Sync.Set();
        }

        // Called by the state machine when the method has thrown an exception.
        internal void SetException(Exception ex)
        {
            ex.LogError();
            _bag.Tcs?.SetException(ex);
            _bag.Sync.Set();
        }

        private void Dispose(bool disposing)
        {
            if (!_bag.IsDisposed)
            {
                lock (_bag.Sync)
                {
                    if (!_bag.IsDisposed) // This disposed flag also tells us the task is finished (when true).
                    {
                        if (disposing)
                        {
                            _bag.Sync.Dispose();
                        }
                        _bag.IsDisposed = true;
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        // Custom awaiter to convert ResponseAsync<T> into Response<T>, when using the await keyword.
        public ResponseAsyncAwaiter<T> GetAwaiter() => new ResponseAsyncAwaiter<T>(this);

        public static implicit operator Task<Response<T>>(ResponseAsync<T> response) => response.AsTask(); // Auto-cast to Task{T}.
    }
}
