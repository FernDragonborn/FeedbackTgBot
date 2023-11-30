namespace FeedbackTgBot;

internal class User
{

    public User(string username, long chatId, States state)
    {
        Username = username;
        ChatId = chatId;
        State = States.Start;
    }
    internal string Username { get; set; }
    internal long ChatId { get; set; }
    public States State { get; set; }
}

public enum States
{
    Start,
    NeedHelp,
    GiveHelp,
    Support,
    GetName,
    GetTel,
    AddEvenet
}