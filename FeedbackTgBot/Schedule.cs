namespace FeedbackTgBot
{
    internal struct Schedule
    {
        public Schedule(DateTime date, string text)
        {
            Date = date;
            Text = text;
        }
        public DateTime Date { get; set; }
        public string Text { get; set; }
    }
}
