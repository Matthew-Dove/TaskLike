using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

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

    /**
     * Use on a method returning a ValueTask{Response{T}} type.
     * [AsyncMethodBuilder(typeof(ResponseAsyncValueTaskCompletionSource{}))]
    **/
    public readonly struct ResponseAsyncValueTaskCompletionSource<T>
    {
        public static ResponseAsyncValueTaskCompletionSource<T> Create() => new ResponseAsyncValueTaskCompletionSource<T>();

        private static readonly bool _isResponseType;

        static ResponseAsyncValueTaskCompletionSource() { _isResponseType = typeof(T).Equals(new Response<T>(default(T)).Value.GetType()); }

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

    /**
     * Use on a method returning a Task{Response{T}} type.
     * [AsyncMethodBuilder(typeof(ResponseAsyncTaskCompletionSource{}))]
    **/
    public readonly struct ResponseAsyncTaskCompletionSource<T>
    {
        public static ResponseAsyncTaskCompletionSource<T> Create() => new ResponseAsyncTaskCompletionSource<T>();

        private static readonly bool _isResponseType;

        static ResponseAsyncTaskCompletionSource() { _isResponseType = typeof(T).Equals(new Response<T>(default(T)).Value.GetType()); }

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

    file sealed class SourceBag<T>
    {
        public T Result;
        public ValueTaskSourceStatus Status;
        public Action<object> Continuation;
        public object State;
    }

    public sealed class ValueTaskSource<T> : IValueTaskSource<T>
    {
        private const int MAX = 16383; // 2^14 - 1

        private static readonly ConcurrentDictionary<short, SourceBag<T>> _sources = new ConcurrentDictionary<short, SourceBag<T>>();
        private static int _token = 0;

        public short GetNextToken()
        {
            if (_token > MAX)
            {
                lock (_sources)
                {
                    if (_token > MAX)
                    {
                        _token = 0;
                    }
                }
            }

            var token = (short)Interlocked.Increment(ref _token);
            _sources.TryAdd(token, new SourceBag<T>());
            return token;
        }

        public void SetResult(short token, T result)
        {
            _sources.TryGetValue(token, out SourceBag<T> source);
            source.Result = result;
        }

        public T GetResult(short token)
        {
            _sources.TryGetValue(token, out SourceBag<T> source);
            _sources.TryRemove(token, out _);
            return source.Result;
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            _sources.TryGetValue(token, out SourceBag<T> source);
            return source.Status;
        }

        public void SetComplete(short token, bool isSuccessful)
        {
            _sources.TryGetValue(token, out SourceBag<T> source);
            source.Status = isSuccessful ? ValueTaskSourceStatus.Succeeded : ValueTaskSourceStatus.Canceled;
            source.Continuation(source.State);
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            _sources.TryGetValue(token, out SourceBag<T> source);
            source.Continuation = continuation;
            source.State = state;
        }
    }

    public readonly struct ResponseAsyncValueTaskSource<T>
    {
        public static ResponseAsyncValueTaskSource<T> Create() => new ResponseAsyncValueTaskSource<T>();

        private static readonly bool _isResponseType;
        private static readonly ValueTaskSource<T> _source;

        static ResponseAsyncValueTaskSource()
        {
            _isResponseType = typeof(T).Equals(new Response<T>(default(T)).Value.GetType());
            _source = new ValueTaskSource<T>();
        }

        public ValueTask<T> Task => new ValueTask<T>(_source, _token);
        private readonly short _token;

        public ResponseAsyncValueTaskSource() { _token = _source.GetNextToken(); }

        public void SetResult(T result)
        {
            _source.SetResult(_token, result);
            _source.SetComplete(_token, true);
        }

        public void SetException(Exception ex)
        {
            ex.LogError();
            if (_isResponseType)
            {
                _source.SetResult(_token, default(T));
                _source.SetComplete(_token, true);
            }
            else
            {
                _source.SetComplete(_token, false);
            }
        }

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
