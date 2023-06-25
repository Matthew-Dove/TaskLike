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
}
