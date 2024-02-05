namespace EFImplicitConversion;

public class Demo : IDemo
{
    private readonly ImplicitCoversionDbContext _context;

    public Demo(ImplicitCoversionDbContext context)
    {
        _context = context;
    }

    public void Run()
    {
        Console.WriteLine("Executing GetBadType");
        GetBadType();

        Console.WriteLine("Executing GetGoodType");
        GetGoodType();
    }

    private void GetBadType()
    {
        var result = _context.BadTypes.Where(bt => bt.SomeVarchar == "Hello World").ToList();
    }

    private void GetGoodType()
    {
        var result = _context.GoodTypes.Where(bt => bt.SomeNVarchar == "Hello World").ToList();
    }
}

public interface IDemo
{
    public void Run();
}
