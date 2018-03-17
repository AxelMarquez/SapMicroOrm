# SapMicroOrm
Retrieve data from SAP tables using LINQ-like syntax.

- It's simple compact and easy to use.
- Performance in mind, it uses [FastMember](https://github.com/mgravell/fast-member) to map to POCOs at lightning speed.
- No limit in terms on how big the table is, if you have used [RFC_READ_TABLE](http://www.se80.co.uk/sapfms/r/rfc_/rfc_read_table.htm) you probably know that rows larger than  512 bytes will result in an exception, with this library forget about this limitation.
- Works out of the box; No need to modify your SAP instance.
- Convention over configuration

### Prerequisites

Have the following SAP .dlls installed in your project:
1. sapnco.dll
2. sapnco_utils.dll
  
To comply with SAP licensing terms, these .dlls aren’t included in this library, you need to manually download them from SAP portal and install them. 
Remember to configure your build options to use x86 or x64 depending on the SAP .dlls architecture.

### Installing

Use [Nuget](https://www.nuget.org/packages/SapMicroOrm/) to download and install this library, there are 2 ways.

- 1st option: In Visual Studio right click your project -> Manage Nuget packages -> Browse. Search and install **SapMicroOrm** package.

- 2nd option: In Visual Studio -> Tools -> Nuget Package Manager -> Package Manager Console. Execute the following command: 

```
Install-Package SapMicroOrm
```

## Getting started
First reference SapMicroOrm in your code:
```
using SapMicroOrm;
```
A table is mapped to a POCO, for this example lets create our POCO for MARA table:
```
public class MARA
{
    public string MATNR { get; set; }
    public string MTART { get; set; }
    public string MATKL { get; set; }

    //Omitted the rest of the columns for simplicity
}
```

Alternatively, a more readable version:
```
[Alias("MARA")]
public class Material
{
    [Alias("MATNR")]
    public string Id { get; set; }

    [Alias("MTART")]
    public string Type { get; set; }

    [Alias("MATKL")]
    public string Group { get; set; }

    //Omitted the rest of the columns for simplicity
}
```
        
Now lets create our usual *RfcDestination* and retrieve some records:
```
var sapConn = RfcDestinationManager.GetDestination(...);
            
List<MARA> materials = sapConn
    .From<MARA>()
    .Where("MATNR = '33916'")
    .SelectAllColumns();
```

That's it! As you can see the name of the table was taken from the name of the Class (MARA) and the name of the columns were taken from the name of the properties. 
If *Alias* is set the name ist taken from there.

Having trouble setting up your project? Refer to [this]() working solution example.

## More examples
#### Retrieve all columns (SELECT * ...)
```
List<MARA> materials = sapConn
    .From<MARA>()
    .SelectAllColumns();
```
#### Retrieve specific columns
```
List<MARA> materials = sapConn
    .From<MARA>()
    .Select("MATNR, MTART"); //You can pass an array of strings too
```
**Note:** Properties not included in the select will have the initial value in the POCO.
#### Add conditions (WHERE ...)
```
List<MARA> materials = sapConn
    .From<MARA>()
    .Where("MATNR = '33916'")
    .SelectAllColumns();
```
#### Joins
Unfortunately, SAP doesn’t provide a way to natively execute joins from a remote client. 
We can simulate it in the following way:
```
List<MARA> materialHeaders = sapConn
    .From<MARA>()
    .SelectAllColumns();

List<MARC> materials = sapConn
    .From<MARC>()
    .Where(materialHeaders.Select(mh => $"MATNR = '{mh.MATNR}'")) // <- Join here
    .SelectAllColumns();
```
#### Order by
```
var materials = sapConn
    .From<MARA>()
    .SelectAllColumns()
    .OrderBy(m => m.MTART); // <- Standard LINQ OrderBy
```
#### Group by
```
var materials = sapConn
    .From<MARA>()
    .SelectAllColumns()
    .GroupBy(m => m.MTART); // <- Standard LINQ GroupBy
```
#### Table and column alias
If you want to have different names in your POCOs use *Alias*:
```
[Alias("MARA")]
public class Material
{
    [Alias("MATNR")]
    public string Id { get; set; }

    [Alias("MTART")]
    public string Type { get; set; }

    [Alias("MATKL")]
    public string Group { get; set; }

    //Omitted the rest of the columns for simplicity
}
```


## Next steps
1. Add support to call arbitrary RFCs
2. Add support to call queries
3. Add tests

## Changelog

1.1.2 Initial version

## License

This project is licensed under the MIT License - see the [license](https://opensource.org/licenses/MIT) for details
