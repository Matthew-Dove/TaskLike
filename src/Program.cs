Try.SetExceptionLogger(Console.Error.WriteLine); // Exceptions in ResponseAsync methods will be logged here.

var playground = new PlayGround();
var msg = await playground.Play();
Console.WriteLine(msg.GetValueOrDefault("Error!"));

Console.WriteLine("EOF");

/**
 * General flow for the task-like type ResponseAsync:
 * 1) Create custom method builder.
 * 2) Foreach await in that method, call OnCompleted on said builder.
 * 3) Outside that method, get the custom awaiter (i.e. Task); and call get result (i.e. await keyword).
 * 4) When the method result returns (or an exception is thrown), inform the awaiter, and we are done with the method builder.
**/

// The result type could be anything you like, it's controlled by the awaiter's GetResult() method; which is returning Response{T} in this case.
