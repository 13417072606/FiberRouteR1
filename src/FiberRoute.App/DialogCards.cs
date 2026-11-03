namespace FiberRoute.App;
public sealed partial class MainForm
{
    Surface DialogCard(string title,Control content,int height)
    {
        var card=new Surface{Height=height,Padding=new Padding(12),BackColor=Color.White,Margin=new Padding(4,5,4,5)};
        content.Dock=DockStyle.Fill;card.Controls.Add(content);
        card.Controls.Add(new Label{Text=title,Dock=DockStyle.Top,Height=30,Font=new Font(Font.FontFamily,11,FontStyle.Bold),ForeColor=Theme.Ink,BackColor=Color.White});
        return card;
    }
    Surface DialogTextCard(string title,string text,int height=92)
    {
        var body=new TextBox{Text=text,Multiline=true,ReadOnly=true,WordWrap=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink};
        return DialogCard(title,body,height);
    }
}
