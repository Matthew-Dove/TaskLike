using System.Runtime.CompilerServices;

namespace TaskLike
{
    public sealed class Box<T> : Alias<T> { public Box(T value) : base(value) { } } // Force T on the heap, so we can mark it volatile; skipping a lock.

    // Bag of things for ResponseAsync{T}, so we can make it a readonly struct.
    public sealed class State<T>
    {
        /**
         * [Volatile WORM access pattern]
         * get: read > isDefault > volatile read
         * set: volatile write
        **/
        public Box<T> Result
        {
            get { return _result == default ? Volatile.Read(ref _result) : _result; }
            set { _result = value; Volatile.Write(ref _result, value); }
        }

        public bool IsCompleted
        {
            get { return _isCompleted == default ? Volatile.Read(ref _isCompleted) : _isCompleted; }
            set { _isCompleted = value; Volatile.Write(ref _isCompleted, value); }
        }

        public TaskCompletionSource<Response<T>> Tcs
        {
            get { return _tcs == default ? Volatile.Read(ref _tcs) : _tcs; }
            set { _tcs = value; Volatile.Write(ref _tcs, value); }
        }

        public Action Continuation
        {
            get { return _continuation == default ? Volatile.Read(ref _continuation) : _continuation; }
            set { _continuation = value; Volatile.Write(ref _continuation, value); }
        }

        private Box<T> _result;
        private bool _isCompleted;
        private TaskCompletionSource<Response<T>> _tcs;
        private Action _continuation;

        public State(T result)
        {
            Result = new Box<T>(result);
            _isCompleted = true;
            Tcs = default;
            _continuation = default;
        }

        public State(Exception ex)
        {
            Result = default;
            _isCompleted = true;
            Tcs = default;
            _continuation = default;
            ex.LogError();
        }

        public State()
        {
            Result = default;
            _isCompleted = false;
            Tcs = default;
            _continuation = default;
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
    public readonly struct ResponseAsync<T> : IEquatable<ResponseAsync<T>>
    {
        private readonly State<T> _state;

        // Constructor to use when you already have the value to set (i.e. T is pre-calculated).
        public ResponseAsync(T result) { _state = new State<T>(result); }

        // Constructor to use when you have an error even before trying to start the calculation.
        public ResponseAsync(Exception ex) { _state = new State<T>(ex); }

        // Constructor to use when you'd like to externally control the state. Useful for object pooling, or caching particular instances.
        public ResponseAsync(State<T> state) { _state = state; }

        // Constructor to use when the result hasn't been calculated yet (i.e. the result, or error will be set later).
        public ResponseAsync() { _state = new State<T>(); }

        // When true, the value is ready to be read.
        internal bool IsCompleted() => _state.IsCompleted;

        internal void OnCompleted(Action continuation) => _state.Continuation = continuation;

        // Called by the awaiter when the result is requested (either manually by getting the awaiter, or when using the await keyword).
        internal Response<T> GetValue()
        {
            if (!_state.IsCompleted) return AsTask().GetAwaiter().GetResult();
            var result = _state.Result;
            return result == default ? new Response<T>() : new Response<T>(result.Value); // If null, then a result was never set for this task.
        }

        // Called by the state machine when the method has returned a result.
        internal void SetValue(T value)
        {
            _state.Result = new Box<T>(value);
            _state.IsCompleted = true;
            _state.Continuation?.Invoke();
            _state.Tcs?.SetResult(Response.Create(value));
        }

        // Called by the state machine when the method has thrown an exception.
        internal void SetException(Exception ex)
        {
            ex.LogError();
            _state.IsCompleted = true;
            _state.Continuation?.Invoke();
            _state.Tcs?.SetResult(new Response<T>());
        }

        // Convert ResponseAsync{T} into a Task{T} type.
        public Task<Response<T>> AsTask()
        {
            var tcs = _state.Tcs;
            if (tcs == default)
            {
                lock (_state)
                {
                    tcs = _state.Tcs;
                    if (tcs == default)
                    {
                        tcs = new TaskCompletionSource<Response<T>>();
                        if (_state.IsCompleted)
                        {
                            var result = _state.Result;
                            if (result != default) tcs.SetResult(Response.Create(result.Value)); // Task is completed.
                            else tcs.SetResult(new Response<T>()); // There is no result, as the task generated an exception, or was canceled.
                        }
                        _state.Tcs = tcs;
                    }
                }
            }
            return tcs.Task;
        }

        // Convert ResponseAsync{T} into a ValueTask{T} type.
        public ValueTask<Response<T>> AsValueTask()
        {
            ValueTask<Response<T>> vt;

            if (_state.IsCompleted)
            {
                var result = _state.Result;
                if (result != default) vt = new(Response.Create(result.Value)); // Task is completed.
                else vt = new(new Response<T>()); // There is no result, as the task generated an exception, or was canceled.
            }
            else
            {
                vt = new(AsTask()); // Task is currently running.
            }

            return vt;
        }

        // Custom awaiter to convert ResponseAsync{T} into Response{T}, when using the await keyword.
        public ResponseAsyncAwaiter<T> GetAwaiter() => new ResponseAsyncAwaiter<T>(this);

        public override int GetHashCode() => _state?.GetHashCode() ?? 0;

        public override bool Equals(object obj) => obj is ResponseAsync<T> response ? Equals(response) : false;

        public bool Equals(ResponseAsync<T> other)
        {
            if (_state is null && other._state is null) return true;
            if (_state is null) return false;
            if (other._state is null) return false;

            if (_state.IsCompleted && other._state.IsCompleted)
            {
                return EqualityComparer<T>.Default.Equals(_state.Result, other._state.Result);
            }

            return false;
        }

        public override string ToString()
        {
            if (_state.IsCompleted)
            {
                T result = _state.Result;
                if (result != null) return result.ToString();
            }
            return string.Empty;
        }

        public static bool operator ==(ResponseAsync<T> left, ResponseAsync<T> right) => left.Equals(right);
        public static bool operator !=(ResponseAsync<T> left, ResponseAsync<T> right) => !left.Equals(right);

        public static implicit operator Task<Response<T>>(ResponseAsync<T> response) => response.AsTask(); // Cast to Task{T}.
        public static implicit operator ValueTask<Response<T>>(ResponseAsync<T> response) => response.AsValueTask(); // Cast to ValueTask{T}.
    }

    public static class ResponseAsync
    {
        // Static helper for the public constructor to set the result.
        public static ResponseAsync<T> FromResult<T>(T result) => new ResponseAsync<T>(result);

        // Static helper for the public constructor to set an error.
        public static ResponseAsync<T> FromException<T>(Exception ex) => new ResponseAsync<T>(ex);

        // Custom Awaiter for a Func returning ResponseAsync{T}.
        public static ResponseAsyncAwaiter<T> GetAwaiter<T>(this Func<ResponseAsync<T>> func) => func().GetAwaiter();
    }
}
