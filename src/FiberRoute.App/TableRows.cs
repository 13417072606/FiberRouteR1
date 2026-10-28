namespace FiberRoute.App;
public sealed partial class MainForm
{
    sealed record ResourceRow(int 序号,int 内部序号,string 始端设施,string 始端区域,string 始端类型,string 终端设施,string 终端区域,string 终端类型,double? 长度米,string 空闲_总芯,string 业务状态,string 操作);
    sealed record TypeRow(string 名称,string 区域,string 类型,string 确认状态,string 来源,string 原因,string 操作);
    sealed record ClusterRow(int 序号,string 内部标识,string 集群名称,string 所属站点,int 成员数,string 类型,string 确认状态,string 路由状态,string 原因,string 成员名单,string 操作);
    sealed record RoomRow(int 序号,string 所属机房,int 设施数,int 已纳入,string 确认状态,string 归并状态,string 操作);
    readonly ComboBox resourceStartArea=new UiComboBox{Width=88},resourceEndArea=new UiComboBox{Width=88},resourceBusiness=new UiComboBox{Width=86},resourceEndType=new UiComboBox{Width=98},nodeArea=new UiComboBox{Width=125};
    bool buildingFilters;
    void RefreshAreaChoices()
    {
        buildingFilters=true;try{var g=store.Graph;FillChoices(resourceStartArea,g.Edges.Select(e=>Area(e.StartRegion)),"全部区域");FillChoices(resourceEndArea,g.Edges.Select(e=>Area(e.EndRegion)),"全部区域");FillChoices(resourceBusiness,g.Edges.Select(e=>Area(e.BusinessStatus)),"全部状态");FillChoices(nodeArea,g.Nodes.Select(n=>Area(n.Region)),"全部区域");RefreshRoomAreaChoices();}finally{buildingFilters=false;}
    }
}
