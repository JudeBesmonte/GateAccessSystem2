using System;
using System.Drawing;
using System.Windows.Forms;
using MaterialSkin;
using MaterialSkin.Controls;

namespace GateAccessSystem2
{
    public partial class Form1 : MaterialForm
    {
        public Form1()
        {
            InitializeComponent();

            var materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);
            materialSkinManager.Theme = MaterialSkinManager.Themes.LIGHT;
            materialSkinManager.ColorScheme = new ColorScheme(
                Primary.BlueGrey800,
                Primary.BlueGrey900,
                Primary.BlueGrey500,
                Accent.LightBlue700,
                TextShade.WHITE
            );

 
            Label label1 = new Label();       
            label1.Location = new Point(20, 20);
            tabPage1.Controls.Add(label1);

            Label label2 = new Label(); 
            label2.Location = new Point(20, 20);
            tabPage2.Controls.Add(label2);
            //hehehehehehe
            //hehehehehehe
            //hehehehehehe
            //hehehehehehe
            //hehehehehehe
            //hehehehehehe
            //hehehehehehe


        }
    }
}

