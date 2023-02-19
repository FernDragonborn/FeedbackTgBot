using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using log4net;
using Newtonsoft.Json;
using static System.Console;

namespace UpWorkTgBot;
public class Crud
{
    static readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };
    static readonly string ApplicationName = "TgFeedbackBot";
    private static readonly string SPREADSHEET_ID_MAIN = DotNetEnv.Env.GetString("SPREADSHEET_ID_MAIN");
    internal static readonly string TABLE_NAME_REQUESTS = DotNetEnv.Env.GetString("TABLE_NAME_REQUESTS");
    internal static readonly string TABLE_NAME_USERS = DotNetEnv.Env.GetString("TABLE_NAME_USERS");
    internal static readonly string TABLE_NAME_EVENTS = DotNetEnv.Env.GetString("TABLE_NAME_EVENTS");
    static string SpreadsheetId = SPREADSHEET_ID_MAIN;
    static string sheet = SPREADSHEET_ID_MAIN;
    static SheetsService service;
    static readonly ILog log = LogManager.GetLogger(typeof(Program));

    public static void DbInit()
    {
        //for ukr lang support
        Console.OutputEncoding = System.Text.Encoding.Default;
        //google authentication shit. Don't touch
        GoogleCredential credential;
        using (var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read))
        {
            credential = GoogleCredential.FromStream(stream)
                .CreateScoped(Scopes);
        }
        service = new SheetsService(new Google.Apis.Services.BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });
        WriteLine("CRUD-module initialization successfull");
    }

    private static string BookAndSheetSelection(int bookId, int sheetId)
    {
        if (bookId == 1)
        {
            SpreadsheetId = SPREADSHEET_ID_MAIN;
            if (sheetId == 1) { sheet = TABLE_NAME_REQUESTS; }
            else if (sheetId == 2) { sheet = TABLE_NAME_USERS; }
            else if (sheetId == 3) { sheet = TABLE_NAME_EVENTS; }
            else log.Error("Error: wrong sheet id");
        }
        else log.Error("Error: wrong book id");

        return sheet;
    }

    readonly static string[] ColumnNames = {
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O",
            "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
            "AA", "AB", "AC", "AD", "AE", "AF", "AG", "AH", "AI", "AJ", "AK", "AL", "AM",
            "AN", "AO", "AP", "AQ", "AR", "AS", "AT", "AU", "AV", "AW", "AX", "AY", "AZ"
        };

    private static void CheckNullInput(dynamic data)
    {
        var ErrNullIput = new Exception("Null input");
        if (data is null) throw ErrNullIput;
    }

    public static int FindFirstFreeRow(int bookId, int sheetId)
    {
        BookAndSheetSelection(bookId, sheetId);
        int i = 2;
        var response = ReadEntry(bookId, sheetId, $"A{i}");
        while (response is not null)
        {
            i += 50;
            response = ReadEntry(bookId, sheetId, $"A{i}");
            if (response is not null) { continue; }

            //if response is null
            while (response is null)
            {
                i -= 10;
                response = ReadEntry(bookId, sheetId, $"A{i}");
                while (response is not null)
                {
                    i += 2;
                    response = ReadEntry(bookId, sheetId, $"A{i}");
                    if (response is not null) { continue; }

                    i -= 1;
                    response = ReadEntry(bookId, sheetId, $"A{i}");
                    if (response is null)
                    {
                        return i--;
                    }
                    else
                    {
                        return i;
                    }

                }
            }
            return i;
        }
        return i;
    }


    //entry = запись
    public static void CreateEntry(int bookId, int sheetId, string firstColumn, List<object> enteriesList)
    {
        CheckNullInput(enteriesList);
        BookAndSheetSelection(bookId, sheetId);
        //for the simplicity this methods helps you to only enter 1st column name
        int index = Array.IndexOf(ColumnNames, firstColumn) + enteriesList.Count;
        string secondColumn = ColumnNames[index];
        var range = $"{sheet}!{firstColumn}:{secondColumn}";

        var valueRange = new ValueRange();
        valueRange.Values = new List<IList<object>> { enteriesList };

        var appendRequest = service.Spreadsheets.Values.Append(valueRange, SpreadsheetId, range);
        appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.
            ValueInputOptionEnum.USERENTERED;
        var appendResponse = appendRequest.Execute();
    }
    public static IList<IList<object>> ReadEntry(int bookId, int sheetId, string cells)
    {
        BookAndSheetSelection(bookId, sheetId);
        string range = $"{sheet}!{cells}";

        SpreadsheetsResource.ValuesResource.GetRequest readRequest =
            service.Spreadsheets.Values.Get(SpreadsheetId, range);
        var response = readRequest.Execute();
        var values = response.Values;
        if (values is not null && values.Count > 0) { return values; }
        else { log.Debug("No data found"); return values; }
    }
    public static void UpdateEntry(int bookId, int sheetId, string cell, List<object> enteriesList)
    {
        CheckNullInput(enteriesList);
        BookAndSheetSelection(bookId, sheetId);
        string range = $"{sheet}!{cell}";
        var valueRange = new ValueRange();

        //{"updated"} only 1 entry is supported
        valueRange.Values = new List<IList<object>> { enteriesList };

        var updateRequest = service.Spreadsheets.Values.Update(valueRange, SpreadsheetId, range);
        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest
            .ValueInputOptionEnum.USERENTERED;
        _ = updateRequest.Execute();
    }
    public static void DeleteEntry(int bookId, int sheetId, string cells)
    {
        BookAndSheetSelection(bookId, sheetId);
        string range = $"{sheet}!{cells}";
        var requestBody = new ClearValuesRequest();

        var deleteRequest = service.Spreadsheets.Values.Clear(requestBody, SpreadsheetId, range);
        var deleteResponse = deleteRequest.Execute();
    }
    public static void ReadEntry_Filter(int bookId, int sheetId)
    {
        BookAndSheetSelection(bookId, sheetId);

        var dataFilters = new List<DataFilter>();
        var dataFilter = new DataFilter();
        dataFilter.A1Range = "Київ";
        dataFilters.Add(dataFilter);
        var requestBody = new BatchGetValuesByDataFilterRequest();
        requestBody.DataFilters = dataFilters;

        SpreadsheetsResource.ValuesResource.BatchGetByDataFilterRequest batchReadRequest =
            service.Spreadsheets.Values.BatchGetByDataFilter(requestBody, SpreadsheetId);

        var response = batchReadRequest.Execute();
        var values = JsonConvert.SerializeObject(response);

        WriteLine(JsonConvert.SerializeObject(values));
        WriteLine(JsonConvert.SerializeObject(response));
    }
}
