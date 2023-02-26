namespace FeedbackTgBot;

internal class User
{

    public User(string username, long chatId, states state)
    {
        Username = username;
        ChatId = chatId;
        State = states.Start;
    }
    public string Username { get; set; }
    public long ChatId { get; set; }
    public states State { get; set; }
}

public enum states
{
    Start,
    NeedHelp,
    GiveHelp,
    Support,
    GetName,
    GetTel,
    AddEvenet
}