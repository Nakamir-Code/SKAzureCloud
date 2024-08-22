// <copyright file="TextDisplay.cs" company="Nakamir, Inc.">
// Copyright (c) Nakamir, Inc. All rights reserved.
// </copyright>
namespace Nakamir.Common;

using System.Collections.Concurrent;
using StereoKit.Framework;
using StereoKit;

public class TextDisplay : IStepper
{
	private static TextDisplay s_instance;
	public static TextDisplay Instance
	{
		get
		{
			s_instance ??= SK.AddStepper<TextDisplay>();
			return s_instance;
		}
	}

	private readonly ConcurrentStack<string> _textFrameStack = new();
	private readonly ConcurrentStack<string> _textStack = new();
	public bool Enabled => true;
	public bool Initialize() => true;
	public void Shutdown() { }
	public void Step()
	{
		Hierarchy.Push(Input.Head.ToMatrix());
		Vec3 textPosition = Vec3.Forward * 0.4f;
		var textRotation = Quat.LookAt(textPosition, Vec3.Zero);
		Pose windowPose = new(textPosition, textRotation);
		UI.WindowBegin("", ref windowPose, Vec2.One, UIWin.Empty, UIMove.None);
		while (_textFrameStack.TryPop(out string msg))
		{
			UI.Text(msg, TextAlign.Center);
		}
		foreach (string msg in _textStack)
		{
			UI.Text(msg, TextAlign.Center);
		}
		UI.WindowEnd();
		Hierarchy.Pop();
	}
	public static void ShowFrame(string text) => Instance._textFrameStack.Push(text);
	public static void PushText(string text) => Instance._textStack.Push(text);
	public static bool PopText() => Instance._textStack.TryPop(out _);
}
