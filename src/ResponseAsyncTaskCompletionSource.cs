using System.Runtime.CompilerServices;

namespace TaskLike
{
    /**
     * Use on a method returning a Task{Response{T}}, or a Task{T} type.
     * [AsyncMethodBuilder(typeof(ResponseAsyncTaskCompletionSource{}))]
    **/
    public readonly struct ResponseAsyncTaskCompletionSource<T>
    {
        public static ResponseAsyncTaskCompletionSource<T> Create() => new ResponseAsyncTaskCompletionSource<T>();

        private static readonly bool _isResponseType;

        static ResponseAsyncTaskCompletionSource() { _isResponseType = typeof(T).Equals(new Response<T>(default(T)).Value?.GetType()); }

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
