using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using LiteDB;

namespace BabbleBot.Helpers;

public class CsvToLiteDbImporter
{
    private readonly string _connectionString;
    private readonly string _collectionName;

    public CsvToLiteDbImporter(string connectionString, string collectionName)
    {
        _connectionString = connectionString;
        _collectionName = collectionName;
    }

    public void ImportCsv(string csvFilePath, Dictionary<string, string> columnMappings)
    {
        using var db = new LiteDatabase(_connectionString);
        var collection = db.GetCollection(_collectionName);
        collection.DeleteAll();

        using var reader = new StreamReader(csvFilePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        });

        // Read headers
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord;

        // Create index for mapped columns if needed
        foreach (var mapping in columnMappings.Values)
        {
            collection.EnsureIndex(mapping);
        }

        // Read and map records
        while (csv.Read())
        {
            var record = new BsonDocument();

            foreach (var header in headers)
            {
                var value = csv.GetField(header);
                
                // If this column should be mapped to a new name
                var newColumnName = columnMappings.TryGetValue(header, out var mappedName) 
                    ? mappedName 
                    : header;

                record[newColumnName] = new BsonValue(value);
            }

            collection.Insert(record);
        }
    }

    public void ImportCsvWithTypedColumns(string csvFilePath, 
        Dictionary<string, (string NewName, Func<string, BsonValue> Converter)> columnMappings)
    {
        using var db = new LiteDatabase(_connectionString);
        var collection = db.GetCollection(_collectionName);
        collection.DeleteAll();

        using var reader = new StreamReader(csvFilePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        });

        // Read headers
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord;

        // Create index for mapped columns
        foreach (var mapping in columnMappings.Values)
        {
            collection.EnsureIndex(mapping.NewName);
        }

        // Read and map records
        while (csv.Read())
        {
            var record = new BsonDocument();

            foreach (var header in headers)
            {
                var value = csv.GetField(header);

                // If this column has a mapping and converter
                if (columnMappings.TryGetValue(header, out var mapping))
                {
                    record[mapping.NewName] = mapping.Converter(value);
                }
                else
                {
                    // Use the original header name and string value
                    record[header] = new BsonValue(value);
                }
            }

            collection.Insert(record);
        }
    }
}