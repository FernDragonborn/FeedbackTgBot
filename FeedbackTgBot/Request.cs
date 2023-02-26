namespace FeedbackTgBot
{
    internal class Request
    {
        public Request(int reqId, long userId, string telegram, states reqType, string reqText)
        {
            if (reqType == states.NeedHelp) { ReqType = "запит на допомогу"; }
            else if (reqType == states.GiveHelp) { ReqType = "пропозиція допомоги"; }
            else if (reqType == states.Support) { ReqType = "інший запит"; }
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
        public states ReqState { get; }
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
