using System.Runtime.CompilerServices;

namespace TaskLike
{
    /**
     * OnCompleted: Called when IsCompleted is false, and the await keyword is used on the {task}.
     * GetResult: Called when the await keyword is used on the {task}, or when manually invoked by: {task}.GetAwaiter().GetResult();.
    **/
    public readonly struct ResponseAsyncAwaiter<T> : ICriticalNotifyCompletion
    {
        public bool IsCompleted => _response.IsCompleted();

        private readonly ResponseAsync<T> _response;

        public ResponseAsyncAwaiter(ResponseAsync<T> response)
        {
            _response = response;
        }

        public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);

        public void OnCompleted(Action continuation)
        {
            _response.Wait(); // Wait for the value to be generated, or for some exception.
            continuation(); // Get the final result.
        }

        public Response<T> GetResult()
        {
            _response.Wait(); // Callers may skip using await, and use this.GetAwaiter().GetResult() instead, so we need to call Wait() here too.
            return _response.GetValue(); // Gets the value, or an invalid response when an exception was set instead of a value.
        }
    }
}
