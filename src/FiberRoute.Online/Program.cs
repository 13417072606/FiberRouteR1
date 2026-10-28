using FiberRoute.Core;

try{Console.OutputEncoding=new System.Text.UTF8Encoding(false);}catch(IOException){}
try
{
    using var cancel=new CancellationTokenSource();Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;cancel.Cancel();};
    switch(args)
    {
        case ["inspect",var workspace]:Console.WriteLine(OnlineDelivery.Inspect(workspace));break;
        case ["prepare",var workspace,var runtime,var data,var version]:Console.WriteLine(OnlineDelivery.Prepare(workspace,runtime,data,version,cancel.Token));break;
        case ["verify",var workspace]:Console.WriteLine(await OnlineDelivery.Verify(workspace,cancel.Token));break;
        default:throw new ArgumentException("用法：inspect <管理工作目录> | prepare <管理工作目录> <运行目录> <数据目录> <数据版本> | verify <管理工作目录>");
    }
    return 0;
}
catch(Exception e){Console.Error.WriteLine("在线更新未完成："+e.Message);return 1;}
