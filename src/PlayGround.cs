﻿using System.Runtime.CompilerServices;

namespace TaskLike
{
    public class PlayGround
    {
        [AsyncMethodBuilder(typeof(ResponseAsyncValueTaskCompletionSource<>))]
        public async ValueTask<Response<string>> Play() // ValueTaskAwaiter
        {
            // Happy path.
            var response = await Sandbox(); // ResponseAsyncAwaiter
            Console.WriteLine($"Response.IsValid: {response.IsValid}, Response.Value: {response.GetValueOrDefault(0)}.");

            // Synchronous example when the value is known.
            Console.WriteLine(ResponseAsync<int>.FromResult(42).GetAwaiter().GetResult().ToString());

            // Error handling.
            await ThrowError01(); // No await.
            await ThrowError02(); // With await.

            // Many tasks at once.
            Response<int>[] results = await Task.WhenAll(Sandbox().AsTask(), Sandbox()); // Both explicit, and implicit task conversions exist.

            return Response.Create(string.Empty);
        }

        private static async ResponseAsync<int> Sandbox() // AsyncMethodBuilder
        {
            await Task.Yield(); // YieldAwaiter
            return await Task.Delay(1).ContinueWith(_ => 1); // TaskAwaiter
        }

        private static async ResponseAsync<int> ThrowError01() => throw new Exception("Error with no await!!!");
        private static async ResponseAsync<int> ThrowError02() { await Task.CompletedTask; throw new Exception("Error WITH await."); }
    }
}
