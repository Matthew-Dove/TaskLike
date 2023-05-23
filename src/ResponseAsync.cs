using System.Runtime.CompilerServices;

namespace TaskLike
{
    file sealed class Box<T> : Alias<T> { public Box(T value) : base(value) { } } // Force T on the heap, so we can mark it volatile; and skip a lock.

    /**
    * General flow for the task-like type ResponseAsync{T}:
    * 1) Create the custom async method builder.
    * 2) Foreach await in that method, call "OnCompleted" on said builder.
    * 3) Outside that method, get the custom awaiter (i.e. the Task); and call get result (i.e. the await keyword).
    * 4) When the method result returns (or an exception is thrown / task is canceled), inform the awaiter the task is completed; and the result is ready.
    **/
    [AsyncMethodBuilder(typeof(ResponseAsyncMethodBuilder<>))]
    public sealed class ResponseAsync<T> : IDisposable
    {
        private volatile Box<T> _box;
        private readonly ManualResetEventSlim _sync;
        private bool _isDisposed;
        private volatile TaskCompletionSource<Response<T>> _tcs;

        // Use this constructor when you already have the value to set (i.e. T is pre-calculated).
        public ResponseAsync(T result)
        {
            _box = new Box<T>(result);
            _sync = null;
            _isDisposed = true;
            _tcs = null;
        }

        internal ResponseAsync()
        {
            _box = null;
            _sync = new ManualResetEventSlim(false);
            _isDisposed = false;
            _tcs = null;
        }

        // Static helper for the public constructor to set the result.
        public static ResponseAsync<T> FromResult(T result) => new ResponseAsync<T>(result);

        // Convert into a Task{T} type.
        public Task<Response<T>> AsTask()
        {
            if (_tcs == null)
            {
                lock (this) // The user calls this (from who knows where, how many times), and we only want the tcs created once.
                {
                    if (_tcs == null)
                    {
                        _tcs = new TaskCompletionSource<Response<T>>();
                        if (_isDisposed)
                        {
                            var box = _box;
                            if (box != null) _tcs.SetResult(Response.Create(box.Value)); // Task is already completed.
                            else _tcs.SetCanceled(); // There is no result, as the task generated an exception, or was canceled.

                        }
                    }
                }
            }
            return _tcs.Task;
        }

        // When true, the value is ready to be read.
        internal bool IsCompleted() { if (_isDisposed) return true; lock (this) { return _isDisposed; } } // Try lock free read first.

        // Called by the awaiter, after the state machine has finished, and before the result has been calculated (when using the await keyword).
        internal void Wait()
        {
            if (_isDisposed) return; // Try lock free read first.
            lock (this) { if (_isDisposed) return; }
            _sync.Wait();
            Dispose(disposing: true);
        }

        // Called by the awaiter when the result is requested (either manually by getting the awaiter, or when using the await keyword).
        internal Response<T> GetValue()
        {
            var box = _box;
            return box == null ? new Response<T>() : new Response<T>(box.Value); // If null, then a result was never set for this task.
        }

        // Called by the state machine when the method has returned a result.
        internal void SetValue(T value)
        {
            _box = new Box<T>(value);
            _tcs?.SetResult(Response.Create(value));
            _sync.Set();
        }

        // Called by the state machine when the method has thrown an exception.
        internal void SetException(Exception ex)
        {
            ex.LogError();
            _tcs?.SetException(ex);
            _sync.Set();
        }

        private void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                lock (this)
                {
                    if (!_isDisposed) // This disposed flag also tells us the task is finished (when true).
                    {
                        if (disposing)
                        {
                            _sync.Dispose();
                        }
                        _isDisposed = true;
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
