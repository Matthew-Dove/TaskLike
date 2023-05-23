using System.Runtime.CompilerServices;

namespace TaskLike
{
    /**
     * Call order from the state machine:
     * 1) Create(): gets a instance.
     * 2) Start(): entry to the method.
     * 3) AwaitOnCompleted(): invoked on each occurrence of the await keyword (you can wrap the MoveNext() to setup / teardown things between awaits).
     * 4) SetResult(): exit of the method.
     * 5) SetException(): when some runtime error is thrown (including canceled tasks); in this case SetResult() is not invoked.
    **/
    public readonly struct ResponseAsyncMethodBuilder<T>
    {
        public static ResponseAsyncMethodBuilder<T> Create() => new ResponseAsyncMethodBuilder<T>();

        public ResponseAsync<T> Task => _response;
        private readonly ResponseAsync<T> _response;

        public ResponseAsyncMethodBuilder() { _response = new ResponseAsync<T>(); }

        public void SetResult(T result) => _response.SetValue(result);

        public void SetException(Exception ex) => _response.SetException(ex);

        public void SetStateMachine(IAsyncStateMachine _) { }

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
            => stateMachine.MoveNext();

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => awaiter.OnCompleted(stateMachine.MoveNext);

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => awaiter.UnsafeOnCompleted(stateMachine.MoveNext);
    }

    // Use on a method returning a ValueTask<Response<T>> - [AsyncMethodBuilder(typeof(ResponseAsyncValueTaskCompletionSource<>))]
    public readonly struct ResponseAsyncValueTaskCompletionSource<T>
    {
        public static ResponseAsyncValueTaskCompletionSource<T> Create() => new ResponseAsyncValueTaskCompletionSource<T>();

        private static readonly bool _isResponseType;

        static ResponseAsyncValueTaskCompletionSource()
        {
            var t = typeof(T);
            _isResponseType = t.IsGenericType && !t.IsClass && t.GenericTypeArguments.Length == 1 && t.Name.StartsWith("Response");
        }

        public ValueTask<T> Task => new ValueTask<T>(_tcs.Task);

        private readonly TaskCompletionSource<T> _tcs;

        public ResponseAsyncValueTaskCompletionSource() { _tcs = new TaskCompletionSource<T>(); }

        public void SetResult(T result) => _tcs.SetResult(result);

        public void SetException(Exception ex) { ex.LogError(); if (_isResponseType) _tcs.SetResult(default(T)); else _tcs.SetException(ex); }

        public void SetStateMachine(IAsyncStateMachine _) { }

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
            => stateMachine.MoveNext();

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => awaiter.OnCompleted(stateMachine.MoveNext);

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => awaiter.UnsafeOnCompleted(stateMachine.MoveNext);
    }

    // Use on a method returning a Task<Response<T>> - [AsyncMethodBuilder(typeof(ResponseAsyncTaskCompletionSource<>))]
    public readonly struct ResponseAsyncTaskCompletionSource<T>
    {
        public static ResponseAsyncTaskCompletionSource<T> Create() => new ResponseAsyncTaskCompletionSource<T>();

        private static readonly bool _isResponseType;

        static ResponseAsyncTaskCompletionSource()
        {
            var t = typeof(T);
            _isResponseType = t.IsGenericType && !t.IsClass && t.GenericTypeArguments.Length == 1 && t.Name.StartsWith("Response");
        }

        public Task<T> Task => _tcs.Task;

        private readonly TaskCompletionSource<T> _tcs;

        public ResponseAsyncTaskCompletionSource() { _tcs = new TaskCompletionSource<T>(); }

        public void SetResult(T result) => _tcs.SetResult(result);

        public void SetException(Exception ex) { ex.LogError(); if (_isResponseType) _tcs.SetResult(default(T)); else _tcs.SetException(ex); }

        public void SetStateMachine(IAsyncStateMachine _) { }

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
            => stateMachine.MoveNext();

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => awaiter.OnCompleted(stateMachine.MoveNext);

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => awaiter.UnsafeOnCompleted(stateMachine.MoveNext);
    }
}
