namespace FeedbackTgBot
{
    internal class Request
    {
        public Request(int reqId, long userId, string telegram, States reqType, string reqText)
        {
            if (reqType == States.NeedHelp) { ReqType = "запит на допомогу"; }
            else if (reqType == States.GiveHelp) { ReqType = "пропозиція допомоги"; }
            else if (reqType == States.Support) { ReqType = "інший запит"; }
            else { ReqType = "не визначено"; }
            ReqState = reqType;
            ReqId = reqId;
            UserId = userId;
            Telegram = telegram;
            ReqText = reqText;
            //повинні перевизначитись далі
            FirstName = "помилка, не надано";
            SecondName = "помилка, не надано";
            ThirdName = "помилка, не надано";
            TelNumber = "помилка, не надано";
        }
        public States ReqState { get; }
        public string ReqType { get; }
        public int ReqId { get; }
        public string FirstName { get; set; }
        public string SecondName { get; set; }
        public string ThirdName { get; set; }
        public string TelNumber { get; set; }
        public string Telegram { get; set; }
        public string ReqText { get; set; }
        public long UserId { get; set; }
    }
}
