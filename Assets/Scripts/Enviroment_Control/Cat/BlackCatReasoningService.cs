using System;

public enum BlackCatGuessRoute
{
    Unresolved,
    Boss1,
    Boss2
}

public readonly struct BlackCatFinalGuessResult
{
    public readonly BlackCatGuessRoute Route;
    public readonly string Message;

    public BlackCatFinalGuessResult(BlackCatGuessRoute route, string message)
    {
        Route = route;
        Message = message;
    }
}

public interface IBlackCatReasoningService
{
    void Ask(string question, Action<string> onSuccess, Action<string> onError);

    void SubmitFinalGuess(
        string guess,
        Action<BlackCatFinalGuessResult> onSuccess,
        Action<string> onError);
}

public interface IBlackCatPuzzleInfo
{
    string PuzzleSurface { get; }
}
