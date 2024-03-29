﻿using log4net;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FeedbackTgBot;
internal class Telegram
{
    #region Initializtion fields
    static readonly ILog log = LogManager.GetLogger(typeof(Program));

    private static readonly string TOKEN = DotNetEnv.Env.GetString("TG_TOKEN");
    private static readonly long ADMIN_TOKEN = Convert.ToInt64(DotNetEnv.Env.GetString("ADMIN_TOKEN"));
    Dictionary<States, ReplyKeyboardMarkup> statesDict = CreateMenuDictionary();
    Dictionary<long, User> usersDic = new();
    Dictionary<long, Request> reqDic = new();
    SortedDictionary<DateTime, Schedule> eventSrtDic = new();

    int workingRowRequests = Crud.FindFirstFreeRow(1, 1);
    int lastRowEvents = Crud.FindFirstFreeRow(1, 3);

    private readonly TelegramBotClient botClient = new(TOKEN);
    public Func<ITelegramBotClient, Exception, CancellationToken, Task> HandlePollingErrorAsync { get; private set; }
    public Func<ITelegramBotClient, Update, CancellationToken, Task> HandleUpdateAsync { get; private set; }
    readonly ReceiverOptions receiverOptions = new()
    {
        AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
    };
    readonly CancellationTokenSource cts = new();
    private readonly CancellationToken cancellationToken;
    #endregion

    internal async Task Init()
    {
        /// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
        botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );

        var me = await botClient.GetMeAsync();
        log.Info($"Start listening for @{me.Username}");

        await LoadEvents();

        await LoadUsers();

        await SendMessageAsync(Convert.ToInt64(ADMIN_TOKEN), $"bot initialized\n{DateTime.Now}");

        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            /// Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Message is not { } message)
                return;
            /// Only process text messages
            if (message.Text is not { } messageText)
                return;
            if (message.From is null || message.From.Username is null)
                return;

            long chatId = message.Chat.Id;

            log.Info($"[TG]: In chat {chatId} received: {messageText}");

            ///initialization of user 
            string startMessage = "Привіт, це бот Громадського Центру для ВПО. Тут можна надати фідбек або отримати допомогу. Слідуйте меню знинзу 🥰";
            if (messageText == "/start")
            {
                if (usersDic.ContainsKey(chatId))
                {
                    if (reqDic.ContainsKey(chatId)) { reqDic.Remove(chatId); }
                    await SetKeyboard(chatId, statesDict[States.Start], startMessage);
                    usersDic[chatId].State = States.Start;
                }
                else
                {
                    usersDic.Add(chatId, new User(message.From.Username, chatId, States.Start));
                    Crud.CreateEntry(1, 2, "A", new List<object>()
                    {
                        usersDic.Last().Value.ChatId,
                        usersDic.Last().Value.Username,
                    });
                    await SetKeyboard(chatId, statesDict[States.Start], startMessage);
                }
            }
            ///якщо не ствоерний користувач із цим чат айді
            else if (!(usersDic.ContainsKey(chatId)))
            {
                usersDic.Add(chatId, new User(message.From.Username, chatId, States.Start));
                Crud.CreateEntry(1, 2, "A", new List<object>()
                {
                    usersDic.Last().Value.ChatId,
                    usersDic.Last().Value.Username,
                });
                await SetKeyboard(chatId, statesDict[States.Start], "Я не знайшов ваш акаунт у існуючих користувачах, тому створив новий 😊\n\nБудь ласка, повторіть ваш запит");
            }
            ///admin commands
            if (chatId == ADMIN_TOKEN)
            {
                if (messageText == "/addEvent")
                {
                    usersDic[chatId].State = States.AddEvenet;
                    await SendMessageAsync(ADMIN_TOKEN, "Надішліть інформацію про івент так: 2023.02.10 16:30 текст для відображення");
                }
                else if (usersDic[chatId].State == States.AddEvenet)
                {
                    try
                    {
                        var date = DateTime.Parse(messageText.TrimStart().TrimEnd().Substring(0, messageText.IndexOf(":") + 3));
                        if (date < DateTime.Now)
                        {
                            await SendMessageAsync(chatId, "Перевірте чи даат, яку ви хочете використати ще не пройшла");
                            return;
                        }
                        var text = messageText.Substring(messageText.IndexOf(":") + 3);
                        eventSrtDic.Add(date, new Schedule(date, text));
                        Crud.CreateEntry(1, 3, "A",
                            new List<object>() {
                            date.ToString("dd.MM.yyyy HH:mm"), text
                            }
                        );
                    }
                    catch (ArgumentException ex)
                    {
                        await SendMessageAsync(chatId, $"Помилка, перевірте чи дата ще не пройшла та чи відповідає формату\nТекст помилки:\n{ex.Message}");
                        return;
                    }
                    await SendMessageAsync(ADMIN_TOKEN, "Додав івент");
                    usersDic[chatId].State = States.Start;
                }
                else if (messageText == "/sendEvents")
                {
                    var sb = new StringBuilder();
                    if (eventSrtDic.Count != 0)
                    {
                        foreach (var eventObj in eventSrtDic)
                        {
                            sb.AppendLine(eventObj.Value.Date.ToString("dd.MM.yyyy HH:mm"));
                            sb.AppendLine(eventObj.Value.Text);
                            sb.AppendLine();
                        }
                    }
                    else { sb.Append("нема івентів"); }
                    await SendMessageAsync(ADMIN_TOKEN, sb.ToString());
                }
                else if (messageText == "/updateEvents")
                {
                    eventSrtDic.Clear();
                    try { await LoadEvents(); }
                    catch (Exception ex)
                    {
                        await SendMessageAsync(chatId, $"Виниклаа помилка: {ex.Message}");
                        return;
                    }
                    await SendMessageAsync(chatId, "Оновив івенти");
                }
                else if (messageText == "/help")
                {
                    await SendMessageAsync(chatId, "Список команд:\n/sendEvents - відображає список усіх івентів\n/addEvent - додати івент");
                }
                //else if (messageText == "/sendEnv")
                //{
                //    await using Stream stream = System.IO.File.OpenRead(@".env");
                //    Message sendFile = await botClient.SendDocumentAsync(
                //        chatId: chatId,
                //        document: new InputOnlineFile(content: stream, fileName: $".env")
                //        );
                //    log.Info("sended .env to admin");
                //}
                else
                {
                    await SendMessageAsync(chatId, "Не вдалося опрацювати команду, перевірте чи нема помилок");
                }
            }

            ///user commands
            if (messageText == "/help" && chatId != ADMIN_TOKEN)
            {
                await SendMessageAsync(chatId, "Тут можна надати фідбек або отримтаи допомогу. Слідуйте меню знинзу 🥰");
            }
            else if (messageText == "Мені потрібна допомога ✅")
            {
                usersDic[chatId].State = States.NeedHelp;
                await SendMessageAsync(chatId, "Чекаю ваш запит ✍");
            }
            else if (messageText == "Хочу запропонувати допомогу 🤲")
            {
                usersDic[chatId].State = States.GiveHelp;
                await SendMessageAsync(chatId, "Чекаю вашу пропозицію ✍");
            }
            else if (messageText == "В мене інший запит ❔")
            {
                usersDic[chatId].State = States.Support;
                await SendMessageAsync(chatId, "Чекаю ваш запит ✍");
            }
            else if (messageText == "Найближчі заходи 📅")
            {
                int i = 0;
                var sb = new StringBuilder();
                var sbFormingSrtDic = new SortedDictionary<DateTime, Schedule>();
                sb.AppendLine("Найближчі заходи: \n");
                if (eventSrtDic.Count != 0)
                {
                    foreach (var eventObj in eventSrtDic)
                    {
                        if (eventObj.Value.Date < DateTime.Now) { continue; }
                        if (i > 1 && eventObj.Value.Date < DateTime.Now.AddDays(7)) { continue; }
                        sbFormingSrtDic.Add(eventObj.Key, eventObj.Value);
                        i++;
                    }
                    foreach (var eventObj in sbFormingSrtDic)
                    {
                        sb.Append("<b>");
                        sb.Append(eventObj.Value.Date.ToString("dddd, dd MMMM, HH:mm"));
                        sb.AppendLine("</b>");
                        sb.AppendLine(eventObj.Value.Text.TrimStart());
                        sb.AppendLine();
                    }
                }
                else { sb.Append("нема івентів"); }
                await SendMessageAsync(chatId, sb.ToString());
            }

            ///якщо не пункт меню
            else
            {
                ///якщо був використаний не визначений стан
                if (!(Enum.IsDefined(usersDic[chatId].State)))
                {
                    await SendMessageAsync(chatId, "Виникла внутрішня помилка. Спробуйте обрати пункт із меню, якщо помилка не пропаде, то зверністья у підтримку 😢");
                    log.Error($"Помилка: {chatId} не мав визначеного usersDic[chatId].State та відправив повідомлення із текстом:\n{messageText}");
                }
                ///якщо не був обраний пункт із меню 
                else if (usersDic[chatId].State == States.Start && chatId != ADMIN_TOKEN)
                {
                    await SendMessageAsync(chatId, "Будь ласка, оберіть пункт з меню, який відповідає вашему запиту");
                }
                else if (usersDic[chatId].State == States.NeedHelp || usersDic[chatId].State == States.GiveHelp || usersDic[chatId].State == States.Support)
                {
                    await SendMessageAsync(chatId, "Дякую за звернення, я передав ваше повідомлення в гуманітарний штаб 😊\n\nБудь ласка напишіть ваші ПІБ в форматі: Мельник Василій Петрович");
                    ///workingRowRequests - 1 тому що айді на 1 менше, ніж "робоча строка", через шапку таблиці      
                    reqDic.Add(chatId, new Request(workingRowRequests - 1, chatId, $"@{message.From.Username}", usersDic[chatId].State, messageText));
                    usersDic[chatId].State = States.GetName;
                }
                else if (usersDic[chatId].State == States.GetName)
                {
                    string[] PIB = messageText.TrimStart().TrimEnd().Split();
                    if (PIB.Length != 3)
                    {
                        await SendMessageAsync(chatId, "Перевірте чи правильно надіслали свої ПІБ, якщо маєте подвійне ім'я/прізвище, то будь ласка, напишіть їх через дефіс: Ганна-Марія");
                        return;
                    }

                    usersDic[chatId].State = States.GetTel;
                    reqDic[chatId].FirstName = PIB[0];
                    reqDic[chatId].SecondName = PIB[1];
                    reqDic[chatId].ThirdName = PIB[2];

                    await SendMessageAsync(chatId, "Надішліть номер телефону у форматі: 380661234567");
                }
                else if (usersDic[chatId].State == States.GetTel)
                {
                    messageText = messageText.TrimStart().TrimEnd();
                    if (!(Regex.IsMatch(messageText, "^\\+?[0-9]{12}$")))
                    {
                        await SendMessageAsync(chatId, "Ваш номер телефону не співпав із форматом, перевірте правильність написання та спробуйте ще раз");
                        return;
                    }
                    if (messageText[0] == '+') { _ = messageText.Remove(0, 1); }
                    usersDic[chatId].State = States.Start;
                    reqDic[chatId].TelNumber = messageText;

                    var reqData = new List<object>()
                    {
                        reqDic[chatId].ReqId,
                        reqDic[chatId].ReqType,
                        reqDic[chatId].UserId,
                        reqDic[chatId].FirstName,
                        reqDic[chatId].SecondName,
                        reqDic[chatId].ThirdName,
                        reqDic[chatId].TelNumber,
                        reqDic[chatId].Telegram,
                        reqDic[chatId].ReqText
                    };
                    Crud.CreateEntry(1, 1, "A", reqData);
                    workingRowRequests++;
                    await SendMessageAsync(ADMIN_TOKEN, CreateRequestMessage(message, reqDic[chatId]));
                    reqDic.Remove(chatId);
                    await SendMessageAsync(chatId, "Дякую за ваші контактні дані! Із вами зв'яжуться з приводу вашого запиту");
                }
                else
                {
                    if (chatId == ADMIN_TOKEN) { return; }
                    //await SendMessageAsync(ADMIN_TOKEN, CreateRequestMessage(usersDic[chatId], message, usersDic[chatId].State));
                    //await SendMessageAsync(chatId, "Дякую за звернення, я передав ваше повідомлення в гуманітарний штаб 😊");
                    usersDic[chatId].State = States.Start;
                    await SendMessageAsync(chatId, "Якась помилка, будь ласка, знову оберіть пункт меню та повторіть запит 😥");
                }
            }
        }
        Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            log.Error(ErrorMessage);
            return Task.CompletedTask;
        }
    }

    private async Task LoadEvents()
    {
        lastRowEvents = Crud.FindFirstFreeRow(1, 3);
        var eventsList = Crud.ReadEntry(1, 3, $"A2:B{lastRowEvents}");
        if (eventsList is not null)
            for (int i = 0; i < eventsList.Count; i++)
            {
                var date = DateTime.ParseExact((string)eventsList[i][0], "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                try { eventSrtDic.Add(date, new Schedule(date, (string)eventsList[i][1])); }
                catch (ArgumentException ex) { await SendMessageAsync(ADMIN_TOKEN, $"Помилка бази даних в листі {Crud.TABLE_NAME_EVENTS}. Текст помилки:\n{ex.Message}"); }
            }
    }

    private async Task LoadUsers()
    {
        var usersList = Crud.ReadEntry(1, 2, $"A2:B{Crud.FindFirstFreeRow(1, 2)}");
        if (usersList is not null)
            foreach (var user in usersList)
            {
                try { usersDic.Add(Convert.ToInt64(user[0]), new User((string)user[1], Convert.ToInt64(user[0]), States.Start)); }
                catch (ArgumentException ex) { await SendMessageAsync(ADMIN_TOKEN, $"Помилка бази даних в листі {Crud.TABLE_NAME_USERS}. Текст помилки:\n{ex.Message}"); }
            }
    }

    public static string CreateRequestMessage(Message message, Request req)
    {
        States state = req.ReqState;
        if (message.From is null) { return $"Помилка: немає даних\nАйді користувача: {message.Chat.Id}\nЗверніться до адміністратора"; }
        var sb = new StringBuilder();
        //додати на що запит
        if (state == States.NeedHelp) { sb.AppendLine("<b>Класс</b>: #запит_на_допомогу"); }
        else if (state == States.GiveHelp) { sb.AppendLine("<b>Класс</b>: #пропозиція_допомоги"); }
        else if (state == States.Support) { sb.AppendLine("<b>Класс</b>: #інший_запит"); }
        sb.AppendLine($"<b>Від</b>: {message.From.FirstName} {message.From.LastName} | @{message.From.Username}");
        //додати підтягування номеру телефону із контакту по запиту або об'єкта юзера
        sb.Append("<b>Номер телефону</b>: ").AppendLine(req.TelNumber);
        sb.AppendLine("<b>Текст запиту</b>: ");
        sb.AppendLine(req.ReqText);
        return sb.ToString();
    }
    public async Task SendMessageAsync(long chatId, string messageText)
    {
        _ = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: messageText,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }
    public async Task SetKeyboard(long chatId, ReplyKeyboardMarkup replyKeyboardMarkup, string message)
    {
        _ = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: message,
            replyMarkup: replyKeyboardMarkup,
            cancellationToken: cancellationToken);
    }
    public static Dictionary<States, ReplyKeyboardMarkup> CreateMenuDictionary()
    {
        var statesDic = new Dictionary<States, ReplyKeyboardMarkup>
        {
            {
                States.Start,
                new ReplyKeyboardMarkup(new[] {
                    new KeyboardButton[] {"Мені потрібна допомога ✅", "Хочу запропонувати допомогу 🤲"},
                    new KeyboardButton[] {"В мене інший запит ❔", "Найближчі заходи 📅" }
                })
                { ResizeKeyboard = true }
            }
        };
        return statesDic;
    }

}

