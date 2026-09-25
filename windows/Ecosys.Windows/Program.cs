using System.Drawing.Drawing2D;
using Ecosys.Windows.Transport;

ApplicationConfiguration.Initialize();
Application.Run(new EcosysForm());

sealed class EcosysForm : Form
{
    readonly BluetoothTransport transport = new();
    readonly Label btLabel = new();
    readonly DotControl btDot = new();
    readonly ListBox log = new();

    static readonly Color Navy = Color.FromArgb(18,58,94);
    static readonly Color Green = Color.FromArgb(45,143,92);
    static readonly Color Blue = Color.FromArgb(47,111,176);
    static readonly Color Text = Color.FromArgb(22,50,74);
    static readonly Color Muted = Color.FromArgb(91,107,116);
    static readonly Color Page = Color.FromArgb(233,237,240);

    public EcosysForm()
    {
        Text = "Ecosys";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 820);
        MinimumSize = new Size(520, 820);
        BackColor = Page;
        DoubleBuffered = true;

        var root = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        Controls.Add(root);

        var title = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(246,248,249) };
        title.MouseDown += DragWindow;
        root.Controls.Add(title);

        var miniLogo = new EcosysLogo { Location = new Point(12,11), Size = new Size(18,18) };
        title.Controls.Add(miniLogo);
        var titleText = new Label { Text="Ecosys", AutoSize=true, Font=new Font("Segoe UI",8.5f), ForeColor=Color.FromArgb(58,70,77), Location=new Point(38,12) };
        title.Controls.Add(titleText);
        AddWindowButton(title, "—", 388, Minimize);
        AddWindowButton(title, "□", 432, MaximizeRestore);
        AddWindowButton(title, "×", 476, Close);

        var hero = new GradientPanel { Location=new Point(0,40), Size=new Size(520,185) };
        root.Controls.Add(hero);
        var logo = new EcosysLogo { Location=new Point(22,22), Size=new Size(64,64), DrawRing=true };
        hero.Controls.Add(logo);
        hero.Controls.Add(new Label { Text="Ecosys", AutoSize=true, Font=new Font("Segoe UI",16), ForeColor=Color.FromArgb(234,242,247), Location=new Point(100,31) });
        hero.Controls.Add(new Label { Text="PRIVATE. DIRECT. YOURS.", AutoSize=true, Font=new Font("Segoe UI",8), ForeColor=Color.FromArgb(169,214,196), Location=new Point(102,58) });

        var bt = new RoundPanel { Location=new Point(22,106), Size=new Size(476,48), Fill=Color.FromArgb(42,72,93), Radius=10 };
        hero.Controls.Add(bt);
        bt.Controls.Add(new Label { Text="♢", AutoSize=true, Font=new Font("Segoe UI",15), ForeColor=Color.FromArgb(234,242,247), Location=new Point(13,13) });
        btLabel.Text="Bluetooth: bereit"; btLabel.AutoSize=true; btLabel.Font=new Font("Segoe UI",9); btLabel.ForeColor=Color.FromArgb(234,242,247); btLabel.Location=new Point(40,15); bt.Controls.Add(btLabel);
        btDot.Location=new Point(454,20); btDot.Size=new Size(8,8); btDot.Fill=Color.FromArgb(95,214,138); bt.Controls.Add(btDot);

        var contentPanel = new Panel { Location=new Point(0,225), Size=new Size(520,595), BackColor=Color.White };
        root.Controls.Add(contentPanel);

        Section(contentPanel,"THIS DEVICE",18,18);
        var local=Card(contentPanel,18,46,484,72);
        local.Controls.Add(new DeviceGlyph { Location=new Point(12,18), Size=new Size(36,36), Kind="pc" });
        local.Controls.Add(new Label { Text=Environment.MachineName, AutoSize=true, Font=new Font("Segoe UI Semibold",9.5f), ForeColor=Text, Location=new Point(62,15) });
        local.Controls.Add(new Label { Text="Dieser PC · Windows", AutoSize=true, Font=new Font("Segoe UI",8), ForeColor=Muted, Location=new Point(62,39) });
        var badge=new Label { Text="Sichtbar", AutoSize=true, Font=new Font("Segoe UI",7.5f), ForeColor=Color.FromArgb(33,122,78), BackColor=Color.FromArgb(223,242,230), Padding=new Padding(7,3,7,3), Location=new Point(414,24) }; local.Controls.Add(badge);

        Section(contentPanel,"NEARBY DEVICES",18,140);
        var refresh=new RoundButton { Text="↻", Location=new Point(468,134), Size=new Size(34,34), Fill=Color.FromArgb(234,242,247), ForeColor=Text, Radius=9 };
        refresh.Click += async (_,_)=>await ScanAsync(); contentPanel.Controls.Add(refresh);

        var phone=Card(contentPanel,18,168,484,64);
        phone.Controls.Add(new DeviceGlyph { Location=new Point(12,14), Size=new Size(36,36), Kind="phone" });
        phone.Controls.Add(new Label { Text="iPhone", AutoSize=true, Font=new Font("Segoe UI Semibold",9), ForeColor=Text, Location=new Point(62,12) });
        phone.Controls.Add(new Label { Text="In Reichweite", AutoSize=true, Font=new Font("Segoe UI",8), ForeColor=Muted, Location=new Point(62,34) });
        var pc=RoundButton("Verbinden",Blue,Color.White,390,16,82,32); pc.Click += (_,_)=>SetStatus("Verbindung angefordert"); phone.Controls.Add(pc);

        var other=Card(contentPanel,18,240,484,64);
        other.Controls.Add(new DeviceGlyph { Location=new Point(12,14), Size=new Size(36,36), Kind="laptop" });
        other.Controls.Add(new Label { Text="ThinkPad-X1", AutoSize=true, Font=new Font("Segoe UI Semibold",9), ForeColor=Text, Location=new Point(62,12) });
        other.Controls.Add(new Label { Text="In Reichweite", AutoSize=true, Font=new Font("Segoe UI",8), ForeColor=Muted, Location=new Point(62,34) });
        var pc2=RoundButton("Verbinden",Blue,Color.White,390,16,82,32); pc2.Click += (_,_)=>SetStatus("Verbindung angefordert"); other.Controls.Add(pc2);

        var find=RoundButton("Find nearby devices",Blue,Color.White,18,316,484,42); find.Click += async (_,_)=>await ScanAsync(); contentPanel.Controls.Add(find);

        Section(contentPanel,"TEST-VERBINDUNG",18,382);
        var send=RoundButton("Send Hello",Green,Color.White,18,410,484,42);
        send.Click += async (_,_)=>{ await transport.SendAsync(Ecosys.Windows.Protocol.EcosysMessage.Hello($"windows-{Environment.MachineName}",Environment.MachineName,"windows").ToJsonString()); SetStatus("→ Hello gesendet"); };
        contentPanel.Controls.Add(send);

        var statusPanel=new RoundPanel { Location=new Point(18,462), Size=new Size(484,66), Fill=Text, Radius=10 };
        contentPanel.Controls.Add(statusPanel);
        log.Location=new Point(10,8); log.Size=new Size(464,50); log.BorderStyle=BorderStyle.None; log.BackColor=Text; log.ForeColor=Color.FromArgb(207,224,232); log.Font=new Font("Cascadia Code",8); contentPanel.Controls.Add(log);

        contentPanel.Controls.Add(new Label { Text="Weitere Funktionen folgen …", AutoSize=false, TextAlign=ContentAlignment.MiddleCenter, Font=new Font("Segoe UI",8), ForeColor=Color.FromArgb(147,161,168), Location=new Point(18,548), Size=new Size(484,28) });

        transport.StatusChanged += (_,m)=>SetStatus(m);
        transport.MessageReceived += (_,m)=>SetStatus("← "+m);
        Shown += async (_,_)=>await ScanAsync();
        FormClosed += async (_,_)=>await transport.DisposeAsync();
    }

    void SetStatus(string message)
    {
        if(InvokeRequired){BeginInvoke(()=>SetStatus(message));return;}
        btLabel.Text=message.Length>38 ? message.Substring(0,38) : message;
        log.Items.Add(message);
        if(log.Items.Count>4) log.Items.RemoveAt(0);
    }

    async Task ScanAsync()
    {
        SetStatus("Suche nach Ecosys-Geräten …");
        try { await transport.StartAsync(); } catch(Exception ex){ SetStatus("Scan: "+ex.Message); }
    }

    static void Section(Control p,string text,int x,int y)=>p.Controls.Add(new Label{Text=text,AutoSize=true,Font=new Font("Segoe UI Semibold",8),ForeColor=Text,Location=new Point(x,y)});
    static RoundPanel Card(Control p,int x,int y,int w,int h){var c=new RoundPanel{Location=new Point(x,y),Size=new Size(w,h),Fill=Color.FromArgb(245,248,249),Radius=12};p.Controls.Add(c);return c;}
    static RoundButton RoundButton(string text,Color fill,Color fore,int x,int y,int w,int h)=>new(){Text=text,Location=new Point(x,y),Size=new Size(w,h),Fill=fill,ForeColor=fore,Radius=8,FlatStyle=FlatStyle.Flat};
    static void AddWindowButton(Panel p,string text,int x,Action click){var b=new Label{Text=text,AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,Location=new Point(x,0),Size=new Size(44,40),Font=new Font("Segoe UI",9),ForeColor=Color.FromArgb(91,107,116),Cursor=Cursors.Hand};b.Click+=(_,_)=>click();p.Controls.Add(b);}
    void DragWindow(object? s,MouseEventArgs e){if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xA1,2,0);}}
    void Minimize()=>WindowState=FormWindowState.Minimized;
    void MaximizeRestore()=>WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ReleaseCapture();
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd,int msg,int wp,int lp);

    sealed class GradientPanel:Panel{protected override void OnPaintBackground(PaintEventArgs e){using var b=new LinearGradientBrush(ClientRectangle,Color.FromArgb(18,58,94),Color.FromArgb(28,110,79),45);e.Graphics.FillRectangle(b,ClientRectangle);}}
    sealed class RoundPanel:Panel{public Color Fill{get;set;}=Color.White;public int Radius{get;set;}=10;protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var path=Path();using var b=new SolidBrush(Fill);e.Graphics.FillPath(b,path);using var pen=new Pen(Color.FromArgb(227,233,235));e.Graphics.DrawPath(pen,path);}GraphicsPath Path(){var p=new GraphicsPath();var r=Radius;var d=r*2;p.AddArc(0,0,d,d,180,90);p.AddArc(Width-d,0,d,d,270,90);p.AddArc(Width-d,Height-d,d,d,0,90);p.AddArc(0,Height-d,d,d,90,90);p.CloseFigure();return p;}}
    sealed class RoundButton:Button{public Color Fill{get;set;}=Blue;public int Radius{get;set;}=8;protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var p=new GraphicsPath();var d=Radius*2;p.AddArc(0,0,d,d,180,90);p.AddArc(Width-d,0,d,d,270,90);p.AddArc(Width-d,Height-d,d,d,0,90);p.AddArc(0,Height-d,d,d,90,90);p.CloseFigure();using var b=new SolidBrush(Fill);e.Graphics.FillPath(b,p);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}}
    sealed class DotControl:Control{public Color Fill{get;set;}=Color.LimeGreen;protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var b=new SolidBrush(Fill);e.Graphics.FillEllipse(b,0,0,Width-1,Height-1);}}
    sealed class EcosysLogo:Control{public bool DrawRing{get;set;}protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float r=Math.Min(Width,Height),cx=Width/2f,cy=Height/2f;if(DrawRing)using(var p=new Pen(Color.FromArgb(100,255,255,255),2)){p.DashPattern=new[]{1f,3f};e.Graphics.DrawEllipse(p,2,2,r-4,r-4);}using var b=new SolidBrush(Blue);e.Graphics.FillEllipse(b,cx-r*.32f,cy-r*.32f,r*.64f,r*.64f);using var g=new SolidBrush(Color.FromArgb(63,174,116));e.Graphics.FillPolygon(g,new[]{new PointF(cx-r*.20f,cy-r*.10f),new PointF(cx-r*.02f,cy-r*.20f),new PointF(cx,cy-r*.04f),new PointF(cx-r*.15f,cy+r*.02f)});using var g2=new SolidBrush(Green);e.Graphics.FillPolygon(g2,new[]{new PointF(cx+r*.05f,cy-r*.20f),new PointF(cx+r*.30f,cy-r*.24f),new PointF(cx+r*.28f,cy-r*.03f),new PointF(cx+r*.08f,cy+r*.02f)});}}
    sealed class DeviceGlyph:Control{public string Kind{get;set;}="pc";protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var p=new Pen(Text,2);if(Kind=="phone")using var path=new GraphicsPath(); path.AddArc(12,4,8,8,180,90); path.AddArc(16,4,8,8,270,90); path.AddArc(16,24,8,8,0,90); path.AddArc(12,24,8,8,90,90); path.CloseFigure(); e.Graphics.DrawPath(p,path);else if(Kind=="laptop"){e.Graphics.DrawRectangle(p,8,7,20,13);e.Graphics.DrawLine(p,5,25,31,25);}else{e.Graphics.DrawRectangle(p,7,6,22,15);e.Graphics.DrawLine(p,12,25,24,25);}}}
}
