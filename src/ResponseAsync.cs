using System.Runtime.CompilerServices;

namespace TaskLike
{
    internal sealed class Box<T> : Alias<T> { public Box(T value) : base(value) { } } // Force T on the heap, so we can mark it volatile; and skip a lock.

    [AsyncMethodBuilder(typeof(ResponseAsyncMethodBuilder<>))]
    public sealed class ResponseAsync<T> : IDisposable
    {
        private volatile Box<T> _result;
        private readonly SemaphoreSlim _sync;
        private bool _disposedValue;
        private volatile TaskCompletionSource<Response<T>> _tcs;

        // Use this constructor when you already have the value to set.
        public ResponseAsync(T result)
        {
            _result = new Box<T>(result);
            _sync = null;
            _disposedValue = true;
            _tcs = null;
        }

        internal ResponseAsync()
        {
            _result = null;
            _sync = new SemaphoreSlim(0, 1);
            _disposedValue = false;
            _tcs = null;
        }

        // Static helper for the public constructor to set the result.
        public static ResponseAsync<T> FromResult(T result) => new ResponseAsync<T>(result);

        // Convert into a Task{T} type.
        public Task<Response<T>> AsTask()
        {
            if (_tcs == null)
            {
                lock (this) // The user calls this (from who knows where, how many times), so we need to play on the safe side.
                {
                    if (_tcs == null)
                    {
                        _tcs = new TaskCompletionSource<Response<T>>();
                    }
                }
            }
            return _tcs.Task;
        }

        // When true, the value is ready to be read.
        internal bool IsCompleted() { if (_disposedValue) return true; lock (this) { return _disposedValue; } } // Try lock free read first.

        // Called by the awaiter, after the state machine has finished, and before the result has been calculated (when using the await keyword).
        internal void Wait()
        {
            if (_disposedValue) return; // Try lock free read first.
            lock (this) { if (_disposedValue) return; }
            _sync.Wait();
            Dispose(disposing: true);
        }

        // Called by the awaiter when the result is requested (either manually by getting the awaiter, or when using the await keyword).
        internal Response<T> GetValue() => _result == null ? new Response<T>() : new Response<T>(_result.Value);

        // Called by the state machine when the method has returned a result.
        internal void SetValue(T value)
        {
            _result = new Box<T>(value);
            _tcs?.SetResult(Response.Create(value));
            _sync.Release(1);
        }

        // Called by the state machine when the method has thrown an exception.
        internal void SetException(Exception ex)
        {
            ex.LogError();
            _tcs?.SetException(ex);
            _sync.Release(1);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                lock (this)
                {
                    if (!_disposedValue) // Double-checked locking, this disposed flag also tells us the task is finished (when true).
                    {
                        if (disposing)
                        {
                            _sync.Dispose();
                        }
                        _disposedValue = true;
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
