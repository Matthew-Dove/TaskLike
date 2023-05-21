# TaskLike

![Banner](assets/images/hero-banner.jpg)

## Intro

This mini project is an example of creating a custom `Task<T>` like type, by implementing an Awaiter, and a AsyncMethodBuilder for a custom task-like type.  
Here we create a `Task` type that catches, and logs all errors coming from a `async` function.  

For more info see:  
* [AsyncMethodBuilder](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-10.0/async-method-builders)
* [TaskAwaiter](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.getawaiter)
* [IValueTaskSource](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.sources.ivaluetasksource-1)

## Show me the code!

```cs
public async ValueTask Play() // ValueTaskAwaiter
{
    // Happy path.
    Response<int> response = await Sandbox(); // Custom Awaiter
    // Response is 1 after the await
}

private static async ResponseAsync<int> Sandbox() // Custom AsyncMethodBuilder
{
    await Task.Yield(); // YieldAwaiter
    return await Task.Delay(1).ContinueWith(_ => 1); // TaskAwaiter
}
```

As you can see we have mixed many different sorts of awaiters in here, and they all work together.  
The custom task-like type `ResponseAsync<T>` will create a `Maybe`, or `Option`, or `Result` (*whatever you want to call it*) around the `T` response (*what I call it*) after the `await`.  
This means you will never get a runtime exception from an `async` method, when using the type `ResponseAsync<T>`.  

For example this is fine now:
```cs
public async ValueTask Play()
{
    // Error handling.
    Response<int> err01 = await ThrowError01(); // No await.
    Response<int> err02 = await ThrowError02(); // With await.

    int result = err01 && err02 ? err01 + err02 : -1;
    // result is "-1" here, as both responses failed.
}

private static async ResponseAsync<int> ThrowError01() => throw new Exception("Error with no await!!!");
private static async ResponseAsync<int> ThrowError02() { await Task.CompletedTask; throw new Exception("Error WITH await."); }
```

All errors are sent to your own logging system, which is configured at startup.  
You can also convert `ResponseAsync<T>` into a `Task<Response<T>>`, so you can use `Task's` native functions, like `WhenAll()`:

```cs
        public async ValueTask Play()
        {
            // Many tasks at once.
            Response<int>[] results = await Task.WhenAll(Sandbox().AsTask(), Sandbox()); // Both explicit, and implicit task conversions exist.
            var sum = results.Where(x => x).Sum(x => x);
            // sum is "2" here, as both functions ran; and returned 1 each.
        }

        private static async ResponseAsync<int> Sandbox()
        {
            await Task.Yield();
            return await Task.Delay(1).ContinueWith(_ => 1);
        }
```

If you are concerned about `Where`, and `Sum` both doing the same thing?!?  
Don't worry too much about it, `Where` casts to boolean making sure each `Response` has a value; then `Sum` treats `Response` as standard `ints` from that point.  
This is what it would look like if I took away the implicit type casting: `results.Where(x => x.IsValid).Sum(x => x.Value)`.  
Though that magic has nothing to do with the custom awaiter, or async method builder from this project; that type comes from [ContainerExpressions](https://github.com/Matthew-Dove/ContainerExpressions).  
It's just that this `Response<T>` is the task-like type I'm returning from the `async await` process.  

## Remarks

This very simple project demonstrates some interesting `Task` types we are able to create in `C#`.  
While `ResponseAsync<T>` only cares about handling exceptions, you could make many different types.  
For example you could time the duration between each `await` call for some free audit logging.  
You could modify thread settings before / after each `await` in a function (*i.e. setting the culture, or user, or cache, etc*).  
With the custom awaiters you can convert the type on the right of the `await` into whatever type you like on the left.  
For example you could `Trim` all strings, and set them to uppercase (*idk why you would, but you could!*).  
