using Generator.Icons;


if (args.Length < 3)
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  generate <tabler-icons-repo> <output.txt>");
    Console.WriteLine("  build    <input.txt> <Tabler.Avalonia project dir>");
    return 1;
}

switch (args[0])
{
    case "generate":
        new GenerateCommand { InputPath = args[1], OutputPath = args[2] }.Execute();
        break;
    case "build":
        new BuildCommand { InputPath = args[1], OutputPath = args[2] }.Execute();
        break;
    default:
        Console.WriteLine("Unknown command: " + args[0]);
        return 1;
}

return 0;
