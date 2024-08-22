// <copyright file="AsyncHelper.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Common;

using System.Threading.Tasks;
using System;

/// <summary>
/// Extension methods for System.Threading.Tasks.Task and System.Threading.Tasks.ValueTask
/// </summary> 
public static class AsyncHelper
{
	private static Action<Exception> s_onException;
	private static bool s_shouldAlwaysRethrowException = true;

	/// <summary>
	/// Safely execute the ValueTask without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
	/// </summary>
	/// <param name="task">ValueTask.</param>
	/// <param name="onException">If an exception is thrown in the ValueTask, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
	/// <param name="continueOnCapturedContext">If set to <c>true</c>, continue on captured context; this will ensure that the Synchronization Context returns to the calling thread. If set to <c>false</c>, continue on a different context; this will allow the Synchronization Context to continue on a different thread</param>
	public static void SafeFireAndForget(this ValueTask task, in Action<Exception> onException = null, in bool continueOnCapturedContext = false) => HandleSafeFireAndForget(task, continueOnCapturedContext, onException);

	/// <summary>
	/// Safely execute the ValueTask without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
	/// </summary>
	/// <param name="task">ValueTask.</param>
	/// <param name="onException">If an exception is thrown in the Task, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
	/// <param name="continueOnCapturedContext">If set to <c>true</c>, continue on captured context; this will ensure that the Synchronization Context returns to the calling thread. If set to <c>false</c>, continue on a different context; this will allow the Synchronization Context to continue on a different thread</param>
	/// <typeparam name="TException">Exception type. If an exception is thrown of a different type, it will not be handled</typeparam>
	public static void SafeFireAndForget<TException>(this ValueTask task, in Action<TException> onException = null, in bool continueOnCapturedContext = false) where TException : Exception => HandleSafeFireAndForget(task, continueOnCapturedContext, onException);

	/// <summary>
	/// Safely execute the Task without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
	/// </summary>
	/// <param name="task">Task.</param>
	/// <param name="onException">If an exception is thrown in the Task, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
	/// <param name="continueOnCapturedContext">If set to <c>true</c>, continue on captured context; this will ensure that the Synchronization Context returns to the calling thread. If set to <c>false</c>, continue on a different context; this will allow the Synchronization Context to continue on a different thread</param>
	public static void SafeFireAndForget(this Task task, in Action<Exception> onException = null, in bool continueOnCapturedContext = false) => HandleSafeFireAndForget(task, continueOnCapturedContext, onException);

	/// <summary>
	/// Safely execute the Task without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
	/// </summary>
	/// <param name="task">Task.</param>
	/// <param name="onException">If an exception is thrown in the Task, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
	/// <param name="continueOnCapturedContext">If set to <c>true</c>, continue on captured context; this will ensure that the Synchronization Context returns to the calling thread. If set to <c>false</c>, continue on a different context; this will allow the Synchronization Context to continue on a different thread</param>
	/// <typeparam name="TException">Exception type. If an exception is thrown of a different type, it will not be handled</typeparam>
	public static void SafeFireAndForget<TException>(this Task task, in Action<TException> onException = null, in bool continueOnCapturedContext = false) where TException : Exception => HandleSafeFireAndForget(task, continueOnCapturedContext, onException);

	/// <summary>
	/// Safely execute the ValueTask without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
	/// </summary>
	/// <param name="task">ValueTask.</param>
	/// <param name="callback">Callback method that is called once the task has been awaited.</param>
	/// <param name="onException">If an exception is thrown in the ValueTask, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
	/// <param name="continueOnCapturedContext">If set to <c>true</c>, continue on captured context; this will ensure that the Synchronization Context returns to the calling thread. If set to <c>false</c>, continue on a different context; this will allow the Synchronization Context to continue on a different thread</param>
	public static void SafeFireAndCallback(this ValueTask task, in Action callback, in Action<Exception> onException = null, in bool continueOnCapturedContext = false) => HandleSafeFireAndCallback(task, continueOnCapturedContext, callback, onException);

	/// <summary>
	/// Safely execute the ValueTask without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
	/// </summary>
	/// <param name="task">ValueTask.</param>
	/// <param name="callback">Callback method that is called once the task has been awaited.</param>
	/// <param name="onException">If an exception is thrown in the Task, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
	/// <param name="continueOnCapturedContext">If set to <c>true</c>, continue on captured context; this will ensure that the Synchronization Context returns to the calling thread. If set to <c>false</c>, continue on a different context; this will allow the Synchronization Context to continue on a different thread</param>
	/// <typeparam name="TException">Exception type. If an exception is thrown of a different type, it will not be handled</typeparam>
	public static void SafeFireAndCallback<TException>(this ValueTask task, in Action callback, in Action<TException> onException = null, in bool continueOnCapturedContext = false) where TException : Exception => HandleSafeFireAndCallback(task, continueOnCapturedContext, callback, onException);

	/// <summary>
	/// Safely execute the ValueTask without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
	/// </summary>
	/// <param name="task">ValueTask.</param>
	/// <param name="callback">Callback method that is called once the task has been awaited.</param>
	/// <param name="onException">If an exception is thrown in the Task, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
	/// <param name="continueOnCapturedContext">If set to <c>true</c>, continue on captured context; this will ensure that the Synchronization Context returns to the calling thread. If set to <c>false</c>, continue on a different context; this will allow the Synchronization Context to continue on a different thread</param>
	public static void SafeFireAndCallback<T>(this ValueTask<T> task, in Action<T> callback, in Action<Exception> onException = null, in bool continueOnCapturedContext = false) => HandleSafeFireAndCallback(task, continueOnCapturedContext, callback, onException);

	/// <summary>
	/// Safely execute the Task without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
	/// </summary>
	/// <param name="task">Task.</param>
	/// <param name="callback">Callback method that is called once the task has been awaited.</param>
	/// <param name="onException">If an exception is thrown in the Task, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
	/// <param name="continueOnCapturedContext">If set to <c>true</c>, continue on captured context; this will ensure that the Synchronization Context returns to the calling thread. If set to <c>false</c>, continue on a different context; this will allow the Synchronization Context to continue on a different thread</param>
	public static void SafeFireAndCallback(this Task task, in Action callback, in Action<Exception> onException = null, in bool continueOnCapturedContext = false) => HandleSafeFireAndCallback(task, continueOnCapturedContext, callback, onException);

	/// <summary>
	/// Safely execute the Task without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
	/// </summary>
	/// <param name="task">Task.</param>
	/// <param name="callback">Callback method that is called once the task has been awaited.</param>
	/// <param name="onException">If an exception is thrown in the Task, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
	/// <param name="continueOnCapturedContext">If set to <c>true</c>, continue on captured context; this will ensure that the Synchronization Context returns to the calling thread. If set to <c>false</c>, continue on a different context; this will allow the Synchronization Context to continue on a different thread</param>
	/// <typeparam name="TException">Exception type. If an exception is thrown of a different type, it will not be handled</typeparam>
	public static void SafeFireAndCallback<TException>(this Task task, in Action callback, in Action<TException> onException = null, in bool continueOnCapturedContext = false) where TException : Exception => HandleSafeFireAndCallback(task, continueOnCapturedContext, callback, onException);

	/// <summary>
	/// Safely execute the Task without waiting for it to complete before moving to the next line of code; commonly known as "Fire And Forget". Inspired by John Thiriet's blog post, "Removing Async Void": https://johnthiriet.com/removing-async-void/.
	/// </summary>
	/// <param name="task">Task.</param>
	/// <param name="callback">Callback method that is called once the task has been awaited.</param>
	/// <param name="onException">If an exception is thrown in the Task, <c>onException</c> will execute. If onException is null, the exception will be re-thrown</param>
	/// <param name="continueOnCapturedContext">If set to <c>true</c>, continue on captured context; this will ensure that the Synchronization Context returns to the calling thread. If set to <c>false</c>, continue on a different context; this will allow the Synchronization Context to continue on a different thread</param>
	public static void SafeFireAndCallback<T>(this Task<T> task, in Action<T> callback, in Action<Exception> onException = null, in bool continueOnCapturedContext = false) => HandleSafeFireAndCallback(task, continueOnCapturedContext, callback, onException);

	/// <summary>
	/// Initialize SafeFireAndForget
	///
	/// Warning: When <c>true</c>, there is no way to catch this exception and it will always result in a crash. Recommended only for debugging purposes.
	/// </summary>
	/// <param name="shouldAlwaysRethrowException">If set to <c>true</c>, after the exception has been caught and handled, the exception will always be rethrown.</param>
	public static void Initialize(in bool shouldAlwaysRethrowException = false) => s_shouldAlwaysRethrowException = shouldAlwaysRethrowException;

	/// <summary>
	/// Remove the default action for SafeFireAndForget
	/// </summary>
	public static void RemoveDefaultExceptionHandling() => s_onException = null;

	/// <summary>
	/// Set the default action for SafeFireAndForget to handle every exception
	/// </summary>
	/// <param name="onException">If an exception is thrown in the Task using SafeFireAndForget, <c>onException</c> will execute</param>
	public static void SetDefaultExceptionHandling(in Action<Exception> onException)
	{
		s_onException = onException ?? throw new ArgumentNullException(nameof(onException));
	}

#pragma warning disable VSTHRD100 // Avoid async void methods
	private static async void HandleSafeFireAndForget<TException>(ValueTask valueTask, bool continueOnCapturedContext, Action<TException> onException) where TException : Exception
#pragma warning restore VSTHRD100 // Avoid async void methods
	{
		try
		{
			await valueTask.ConfigureAwait(continueOnCapturedContext);
		}
		catch (TException ex) when (s_onException is not null || onException is not null)
		{
			HandleException(ex, onException);

			if (s_shouldAlwaysRethrowException)
			{
				throw;
			}
		}
	}

#pragma warning disable VSTHRD100 // Avoid async void methods
	private static async void HandleSafeFireAndForget<TException>(Task task, bool continueOnCapturedContext, Action<TException> onException) where TException : Exception
#pragma warning restore VSTHRD100 // Avoid async void methods
	{
		try
		{
			await task.ConfigureAwait(continueOnCapturedContext);
		}
		catch (TException ex) when (s_onException is not null || onException is not null)
		{
			HandleException(ex, onException);

			if (s_shouldAlwaysRethrowException)
			{
				throw;
			}
		}
	}

#pragma warning disable VSTHRD100 // Avoid async void methods
	private static async void HandleSafeFireAndCallback<TException>(ValueTask valueTask, bool continueOnCapturedContext, Action callback, Action<TException> onException) where TException : Exception
#pragma warning restore VSTHRD100 // Avoid async void methods
	{
		try
		{
			await valueTask.ConfigureAwait(continueOnCapturedContext);
			callback?.Invoke();
		}
		catch (TException ex) when (s_onException is not null || onException is not null)
		{
			HandleException(ex, onException);

			if (s_shouldAlwaysRethrowException)
			{
				throw;
			}
		}
	}

#pragma warning disable VSTHRD100 // Avoid async void methods
	private static async void HandleSafeFireAndCallback<TException>(Task task, bool continueOnCapturedContext, Action callback, Action<TException> onException) where TException : Exception
#pragma warning restore VSTHRD100 // Avoid async void methods
	{
		try
		{
			await task.ConfigureAwait(continueOnCapturedContext);
			callback?.Invoke();
		}
		catch (TException ex) when (s_onException is not null || onException is not null)
		{
			HandleException(ex, onException);

			if (s_shouldAlwaysRethrowException)
			{
				throw;
			}
		}
	}

#pragma warning disable VSTHRD100 // Avoid async void methods
	private static async void HandleSafeFireAndCallback<TException, T>(Task<T> task, bool continueOnCapturedContext, Action<T> callback, Action<TException> onException) where TException : Exception
#pragma warning restore VSTHRD100 // Avoid async void methods
	{
		try
		{
			T res = await task.ConfigureAwait(continueOnCapturedContext);
			callback?.Invoke(res);
		}
		catch (TException ex) when (s_onException is not null || onException is not null)
		{
			HandleException(ex, onException);

			if (s_shouldAlwaysRethrowException)
			{
				throw;
			}
		}
	}

#pragma warning disable VSTHRD100 // Avoid async void methods
	private static async void HandleSafeFireAndCallback<TException, T>(ValueTask<T> task, bool continueOnCapturedContext, Action<T> callback, Action<TException> onException) where TException : Exception
#pragma warning restore VSTHRD100 // Avoid async void methods
	{
		try
		{
			T res = await task.ConfigureAwait(continueOnCapturedContext);
			callback?.Invoke(res);
		}
		catch (TException ex) when (s_onException is not null || onException is not null)
		{
			HandleException(ex, onException);

			if (s_shouldAlwaysRethrowException)
			{
				throw;
			}
		}
	}

	private static void HandleException<TException>(in TException exception, in Action<TException> onException) where TException : Exception
	{
		s_onException?.Invoke(exception);
		onException?.Invoke(exception);
	}
}
