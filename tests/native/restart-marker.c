#define UNICODE
#include <windows.h>
static WCHAR path[32768];
void WINAPI MarkerEntry(void){DWORD size=GetModuleFileNameW(NULL,path,32768);while(size&&path[size-1]!=L'\\')--size;path[size]=0;lstrcatW(path,L"restarted.txt");HANDLE file=CreateFileW(path,GENERIC_WRITE,FILE_SHARE_READ,NULL,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,NULL);DWORD written;if(file!=INVALID_HANDLE_VALUE){WriteFile(file,"ok",2,&written,NULL);CloseHandle(file);}ExitProcess(0);}
