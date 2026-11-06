using System.Drawing.Drawing2D;
namespace LocalChat;
// Actual WinForms browser chrome: owner-drawn, keyboard accessible, DPI-scaled.
sealed class RoundedButton:Button {
 bool hover; public int Radius {get;set;}=12;public Color Fill {get;set;}=Color.FromArgb(39,42,48);public Color HoverFill {get;set;}=Color.FromArgb(55,61,72);public Color Stroke {get;set;}=Color.FromArgb(65,71,82);
 public RoundedButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;ForeColor=Color.FromArgb(233,238,247);BackColor=Color.FromArgb(24,27,32);Font=new Font("Segoe UI",10);Cursor=Cursors.Hand;UseVisualStyleBackColor=false;SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
 internal static GraphicsPath Shape(RectangleF r,float radius){float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));var p=new GraphicsPath();p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var r=new RectangleF(1,1,Width-3,Height-3);if(r.Width<=1||r.Height<=1)return;using var shape=Shape(r,Radius*DeviceDpi/96f);using var fill=new SolidBrush(hover&&Enabled?HoverFill:Fill);using var border=new Pen(Focused?Color.FromArgb(132,176,249):Stroke,Focused?2:1);e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(border,shape);var text=Rectangle.Inflate(ClientRectangle,-8,-3);TextRenderer.DrawText(e.Graphics,Text,Font,text,Enabled?ForeColor:Color.FromArgb(128,136,148),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);}
}
sealed class RoundedPanel:Panel {
 public RoundedPanel(){DoubleBuffered=true;BackColor=Color.FromArgb(24,27,32);Padding=new Padding(14,12,14,10);}
 protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;if(Width<4||Height<4)return;using var p=RoundedButton.Shape(new RectangleF(1,1,Width-3,Height-3),14*DeviceDpi/96f);using var f=new SolidBrush(Color.FromArgb(35,39,46));using var pen=new Pen(Color.FromArgb(64,71,83));e.Graphics.FillPath(f,p);e.Graphics.DrawPath(pen,p);}
}
