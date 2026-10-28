#define UNICODE
#define _UNICODE
#include <windows.h>
#if defined(BRAND)
#define PRODUCT L"更换品牌图标"
#define TARGET L"程序\\FiberRoute.Brand.exe"
#elif !defined(PUBLISHER)
#define PRODUCT L"光路调度路由查询"
#define TARGET L"程序\\FiberRouteR1.exe"
#else
#define PRODUCT L"管理方发布工具"
#define TARGET L"程序\\FiberRoute.Publisher.exe"
#endif
static WCHAR root[32768],program[32768],command[65536],data[32768],database[32768];
void WINAPI FiberEntry(void)
{
    DWORD size=GetModuleFileNameW(NULL,root,32768);if(!size||size>=32700){MessageBoxW(NULL,L"无法确定运行目录。",PRODUCT,MB_ICONERROR);ExitProcess(1);}while(size&&root[size-1]!=L'\\')--size;root[size]=0;
    lstrcpyW(program,root);lstrcatW(program,TARGET);if(GetFileAttributesW(program)==INVALID_FILE_ATTRIBUTES){MessageBoxW(NULL,L"缺少程序文件，请完整安装补丁后重试。",PRODUCT,MB_ICONERROR);ExitProcess(2);}
    lstrcpyW(command,L"\"");lstrcatW(command,program);lstrcatW(command,L"\"");
#if !defined(PUBLISHER) && !defined(BRAND)
    lstrcpyW(data,root);lstrcatW(data,L"数据");lstrcpyW(database,data);lstrcatW(database,L"\\resources.db");if(GetFileAttributesW(database)!=INVALID_FILE_ATTRIBUTES){lstrcatW(command,L" --data \"");lstrcatW(command,data);lstrcatW(command,L"\"");}
#endif
    STARTUPINFOW startup={0};PROCESS_INFORMATION process={0};startup.cb=sizeof(startup);
    if(!CreateProcessW(program,command,NULL,NULL,FALSE,0,NULL,root,&startup,&process)){MessageBoxW(NULL,L"启动失败。请确认程序完整、文件未被安全软件隔离，并检查运行目录权限。",PRODUCT,MB_ICONERROR);ExitProcess(3);}CloseHandle(process.hThread);CloseHandle(process.hProcess);ExitProcess(0);
}
