using StarMicronics.StarIO;
using StarMicronics.StarIOExtension;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Xml;
using ZXing;
using ZXing.QrCode;
using ZXing.Common;
using ZXing.QrCode.Internal;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Data;

namespace Project1
{
    class Class1
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool FreeConsole();
        static string[] cmd = { "-i", "-p" };
        static string[] cmdval = { "", "" };

        // 存購買項目明細
        static List<string> products = new List<string>();

        static string appPath;
        static Dictionary<string, string> dr = new Dictionary<string, string>();
        static byte[] command;
        static void Main(string[] args)
        {
            //TEST
            appPath = AppDomain.CurrentDomain.BaseDirectory;
            //隱藏式窗
            FreeConsole();
            
            string vName;
            int poi = -1;

            //分析參數
            for (int i = 0; i < args.Length; i++)
            {
                vName = args[i].ToLower();
                int j;
                //檢查是否為指令
                for (j = 0; j < cmd.Length; j++)
                {
                    if (cmd[j] == vName)
                    {
                        //當符合指令時，跳出迴圈，此時j=指令索引
                        break;
                    }
                }
                //如果j>=指令索引陣列長度(沒找到)，代表不是指令，屬於前一個指令資料的附加字串
                //但是也必須在有前一個指令的狀況下 (poi >= 0)
                if (j == cmd.Length && poi >= 0)
                {
                    //附加到前一個指令的資料中
                    if (cmdval[poi] == "")
                    {
                        cmdval[poi] = args[i];
                    }
                    else
                    {
                        cmdval[poi] = cmdval[poi] + " " + args[i];
                    }
                    //處理下一個
                    continue;
                }
                //poi指定為指令索引位置
                poi = j;
            }

            //檢查參數
            if (!File.Exists(cmdval[0] + ".xml"))
            {
                ErrorLog("資料檔案" + cmdval[0] + ".xml不存在 -- 終止執行");
                return;
            }

            if (!LoadData(cmdval[0] + ".xml"))
            {
                ErrorLog("讀取檔案" + cmdval[0] + ".xml失敗 -- 終止執行");
                return;
            }

            command = CreateBitmapData(Emulation.StarGraphic);
            // 判斷是否列印明細
            if (dr["detail"] == "1")
            {
                //檢查參數
                if (!File.Exists(cmdval[0] + "_Detail.xml"))
                {
                    ErrorLog("資料檔案" + cmdval[0] + "_Detail.xml不存在 -- 終止執行");
                    return;
                }

                if (!LoadData(cmdval[0] + "_Detail.xml"))
                {
                    ErrorLog("讀取檔案" + cmdval[0] + "_Detail.xml失敗 -- 終止執行");
                    return;
                }
                command = appendBytes(CreateBitmapDataDetail(Emulation.StarGraphic));
            }

            string str = System.Text.Encoding.UTF8.GetString(command, 0, command.Length);

            IPort port = null;
            try
            {
                port = Factory.I.GetPort("TCP:"+ cmdval[1], "", 500);
                //like => Factory.I.GetPort("TCP:192.168.1.104","stargraphic;l1",0);
                StarPrinterStatus status = port.BeginCheckedBlock();
                if (status.Offline)
                {
                    Console.WriteLine("OFF1");
                }
                uint writtenLength = port.WritePort(command, 0, (uint)command.Length);
                if (writtenLength != command.Length)
                {
                    Console.WriteLine("WritePort failed.");
                }
                Console.WriteLine("Successful");
            }
            catch (PortException ex)
            {
                 Console.WriteLine(ex);
            }
            finally
            {
                Factory.I.ReleasePort(port);
                Application.Exit();
            }
        }
        public static byte[] appendBytes(byte[] bytes)
        {
            int i = command.Length;
            Array.Resize<byte>(ref command, i + bytes.Length);
            bytes.CopyTo(command, i);
            return command;
        }

        public static byte[] CreateBitmapData(Emulation emulation)
        {
            ICommandBuilder builder = StarIoExt.CreateCommandBuilder(emulation);
            builder.BeginDocument();

            string logoFile = appPath + @"\"+dr["logo"];
            Bitmap logo = (Bitmap)Bitmap.FromFile(logoFile);
            Bitmap newImage = ResizeBitmap(logo, 400, 66);

            builder.AppendBitmap(newImage, false);

            builder.AppendUnitFeed(16);
            String BitmapTitle;
            Font TitleFont;
            Bitmap rasterImage;

            if (dr["atttext"] != "")
            {
                BitmapTitle = "電子發票證明聯補印\n";
                TitleFont = new Font("新細明體", 36);
                rasterImage = CreateBitmapFromString(BitmapTitle, 92.0F, 96.0F, TitleFont,-8);
            }
            else
            {
                BitmapTitle = "電子發票證明聯\n";
                TitleFont = new Font("新細明體", 40);
                rasterImage = CreateBitmapFromString(BitmapTitle, 96.0F, 96.0F, TitleFont, 0);
            }

            builder.AppendBitmap(rasterImage, false);

            String InvoiceTitle = "  "+dr["year"]+"年"+dr["months"]+"月"+"\n";

            InvoiceTitle += "  "+dr["invoice"] +"\n";
            Font InvoiceTitleFont = new Font("新細明體", 40, FontStyle.Bold);
            Bitmap rasterImage2 = CreateBitmapFromString(InvoiceTitle, 96.0F, 96.0F, InvoiceTitleFont,0);
            builder.AppendBitmap(rasterImage2, false);

            //格式
            String DateTime = " "+dr["printtime"] +"    ";
            if(dr["fixtext"] != "")
            {
                DateTime += "格式 "+dr["fixtext"] +"\n";
            }
            else
            {
                DateTime += "\n";
            }

            Font DateTimeFont = new Font("新細明體", 20);
            Bitmap rasterImage3 = CreateBitmapFromString(DateTime, 96.0F, 96.0F, DateTimeFont, 0);
            builder.AppendBitmap(rasterImage3, false);

            String Random_Total = " 隨機碼 "+dr["randcode"] +"       ";
            Random_Total += "總計 "+dr["total"] +"\n";
            Font Random_TotalFont = new Font("新細明體", 20);
            Bitmap rasterImage4 = CreateBitmapFromString(Random_Total, 96.0F, 96.0F, Random_TotalFont, 0);
            builder.AppendBitmap(rasterImage4, false);

            String Seller_Buyer = " 賣方 "+dr["sellerid"]+"   ";

            if (dr["byerid"] != "0000000000")
            {
                Seller_Buyer += "買方 "+ dr["byerid"] + "\n";
            }
            else
            {
                Seller_Buyer += "\n";
            }

            Font Seller_BuyerFont = new Font("新細明體", 20);
            Bitmap rasterImage5 = CreateBitmapFromString(Seller_Buyer, 96.0F, 96.0F, Seller_BuyerFont, 0);
            builder.AppendBitmap(rasterImage5, false);

            builder.AppendUnitFeed(5);

            Image c39Img = GetCode39(dr["barcode"], 50);
            //計算條碼寬度產生的比例(會依照印表機DPI值變化)
            //可列印寬度(兩側留白0.3以上) = 5.7 - (0.3 * 2) = 5.1cm
            float ItoC = 2.54f;
            float dpiX = (float)200;
            float dpiY = (float)200;

            float widthlimit = (5.7f / ItoC) * dpiX;
            float rate = 0f;
            float newWidth = 0f;
            do
            {
                rate++;
                newWidth = c39Img.Width * rate;

            } while ((c39Img.Width * (rate + 1.0f)) <= widthlimit);
            //計算X定位點(條碼置中)
            float newX = (((5.7f / ItoC) * dpiX) / 2.0f) - (newWidth / 2.0f);

            Bitmap mybmp1 = new Bitmap((int)newWidth, 60);
            Graphics gr1 = Graphics.FromImage(mybmp1);
            gr1.DrawImage(c39Img,new Point[] { new Point(0, 0), new Point((int)newWidth, 0), new Point(0, c39Img.Height) });
            gr1.Dispose();
            builder.AppendBitmap(mybmp1, false);

            //QR碼
            //利用matrix來計算產生QR碼的實際Size(去白邊)
            var hints = new Dictionary<EncodeHintType, object> { { EncodeHintType.CHARACTER_SET, "UTF-8" },{ EncodeHintType.QR_VERSION,8 } };
            var matrix = new MultiFormatWriter().encode(dr["qrcode1"], BarcodeFormat.QR_CODE, 140, 140, hints);
            var matrix2 = new MultiFormatWriter().encode(dr["qrcode2"], BarcodeFormat.QR_CODE, 140, 140, hints);

            matrix = CutWhiteBorder(matrix);
            matrix2 = CutWhiteBorder(matrix2);
            //把QR碼實際Size給BarcodeWriter參考產生
            var qr1Writer = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = matrix.Height,
                    Width = matrix.Width,
                    CharacterSet = "utf-8",
                    Margin = 0,
                    ErrorCorrection = ErrorCorrectionLevel.L,
                    QrVersion = 8
                }
            };

            var qr2Writer = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = matrix2.Height,
                    Width = matrix2.Width,
                    CharacterSet = "utf-8",
                    Margin = 0,
                    ErrorCorrection = ErrorCorrectionLevel.L,
                    QrVersion = 8
                }
            };


            //QR碼至此產生的大小為不含白邊的原圖大小
            Image qr1Img = qr1Writer.Write(dr["qrcode1"]);
            Image qr2Img = qr2Writer.Write(dr["qrcode2"]);

            Bitmap mybmp = new Bitmap(600, 180);
            Graphics gr = Graphics.FromImage(mybmp);

            //處理第一張圖片
            gr.DrawImage(qr1Img, new Point[] { new Point(0, 0), new Point(140, 0), new Point(0, 140) });
            gr.DrawImage(qr2Img,new Point[] { new Point(204, 0), new Point(344, 0), new Point(204, 140) });
            //處理第二張圖片

            gr.Dispose();
            builder.AppendBitmapWithAbsolutePosition(mybmp, false, 30);

            // 打統編要接著印交易明細
            if(dr["byerid"] == "0000000000")
            {
                builder.AppendCutPaper(CutPaperAction.PartialCutWithFeed);
            }
            
            builder.AppendPeripheral(PeripheralChannel.No1);
            builder.EndDocument();
            return builder.Commands;
        }
        public static byte[] CreateBitmapDataDetail(Emulation emulation)
        {
            ICommandBuilder builder = StarIoExt.CreateCommandBuilder(emulation);
            builder.BeginDocument();

            String BitmapTitle;
            Font TitleFont;
            Bitmap rasterImage;
            String BitmapDetail;

            BitmapTitle = "交易明細\n";
            TitleFont = new Font("新細明體", 40);

            
            rasterImage = CreateBitmapFromString(BitmapTitle, 96.0F, 96.0F, TitleFont, 96);
            builder.AppendBitmap(rasterImage, false);

            String DateTime = " " + dr["printtime"] + "\n\n";


            Font DateTimeFont = new Font("新細明體", 20);
            Bitmap rasterImage3 = CreateBitmapFromString(DateTime, 96.0F, 96.0F, DateTimeFont, 0);
            builder.AppendBitmap(rasterImage3, false);

            Font DetailFont = new Font("新細明體", 18);
            string subtitle = "品名/數量\t單價\t金額";
            
            string subtitleWithSpace = CalculateSpaceWidth(subtitle, DetailFont, 96.0F, 96.0F, 20);

            BitmapDetail = subtitleWithSpace + "\n";
           
            foreach (string productlist in products)
            {
                string[] lines = productlist.Split('\n');

                foreach (string line in lines)
                {
                    string productWithSpace = CalculateSpaceWidth(line, DetailFont, 96.0F, 96.0F, 20);
                    BitmapDetail += productWithSpace + "\n";
                }
                
            }
            

            
            Bitmap rasterImage4 = CreateBitmapFromString(BitmapDetail, 96.0F, 96.0F, DetailFont, 0,true);
            builder.AppendBitmap(rasterImage4, false);

            String total = "\n總計："+ new string(' ', 10)  + dr["total"] + " 元\n";
            Font totalFont = new Font("新細明體", 18);
            Bitmap rasterImage5 = CreateBitmapFromString(total, 96.0F, 96.0F, totalFont, 0, false);
            builder.AppendBitmap(rasterImage5, false);

            builder.AppendUnitFeed(5);


            builder.AppendCutPaper(CutPaperAction.PartialCutWithFeed);
            builder.AppendPeripheral(PeripheralChannel.No1);
            builder.EndDocument();
            return builder.Commands;
        }
        private static string CalculateSpaceWidth(string sourceString, Font printFont, float xDpi, float yDpi,int spaceAmount)
        {
            // 計算剩餘空白寬度
            string textWithSpace = "";
            string[] textString = sourceString.Split('\t');
            string space = new string(' ', spaceAmount);
            SizeF spaceSize = CaluculateBitmapSize(space, printFont, xDpi, yDpi);

            foreach (var val in textString)
            {
                string text = val;
                SizeF textSize = CaluculateBitmapSize(text, printFont, xDpi, yDpi);
                int width = (int)Math.Round((textSize.Width - spaceSize.Width) / spaceSize.Width);
                int count = spaceAmount - width;
                if (count < 0)
                {
                    count = 0;
                }
                textWithSpace += text + new string(' ', count);
            }
            return textWithSpace;
        }
        static Bitmap ResizeBitmap(Bitmap bmp, int width, int height)
        {
            Bitmap result = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.DrawImage(bmp, 0, 0, width, height);
            }
            return result;
        }
       
        private static SizeF CaluculateBitmapSize(string sourceString, Font printFont, float xDpi, float yDpi)
        {
            SizeF stringSize = new SizeF();
            float width = 0;
            float height = 0;
            Bitmap bitmap = new Bitmap(2000, 2000);
            bitmap.SetResolution(xDpi, yDpi);
            Graphics graphics = Graphics.FromImage(bitmap);

            int count = 0;

            string[] lines = sourceString.Split('\n');

            foreach (string line in lines)
            {
                stringSize = graphics.MeasureString(line, printFont, 2000);
                if (stringSize.Width > width)
                {
                    width = stringSize.Width;
                }

                height = count * printFont.GetHeight(graphics);
                count++;
            }

            return new SizeF(width, height);
        }

        private static Bitmap CreateBitmapFromString(string sourceString, float xDpi, float yDpi,Font printFont, float leftMargin,Boolean Drawline = false)
        {
            StringFormat format = new StringFormat();
            
            float yPos = 0;
            int count = 0;
            float topMargin = 0;

            SizeF bitmapSize = CaluculateBitmapSize(sourceString, printFont, xDpi, yDpi);
           
            Bitmap bitmap = new Bitmap((int)(bitmapSize.Width + leftMargin), (int)(bitmapSize.Height + topMargin));
            bitmap.SetResolution(xDpi, yDpi);
            Graphics graphics = Graphics.FromImage(bitmap);
            
            string[] lines = sourceString.Split('\n');
            
            foreach (string line in lines)
            {
                yPos = topMargin + (count * printFont.GetHeight(graphics));
                graphics.DrawString(line, printFont, Brushes.Black, leftMargin, yPos, format);
                count++;
            }
            
            
            if (Drawline)
            {
                // 總計上方畫線
                Pen blackPen = new Pen(Color.Black, 3);

                PointF point1 = new PointF(0, yPos-2);
                PointF point2 = new PointF(280, yPos-2);
                graphics.DrawLine(blackPen, point1, point2);
            }
            
            graphics.Dispose();
            printFont.Dispose();
            return bitmap;
        }
        
        static Bitmap GetCode39(string strSource, int barHeight)
        {
            int x = 50; //左邊界
            int y = 0; //上邊界
            int WidLength = 2; //粗BarCode長度
            int NarrowLength = 1; //細BarCode長度
            int BarCodeHeight = barHeight; //BarCode高度
            int intSourceLength = strSource.Length;
            string strEncode = "010010100"; //編碼字串 初值為 起始符號 *

            string AlphaBet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%*"; //Code39的字母

            string[] Code39 = //Code39的各字母對應碼
            {
       /* 0 */ "000110100",
       /* 1 */ "100100001",
       /* 2 */ "001100001",
       /* 3 */ "101100000",
       /* 4 */ "000110001",
       /* 5 */ "100110000",
       /* 6 */ "001110000",
       /* 7 */ "000100101",
       /* 8 */ "100100100",
       /* 9 */ "001100100",
       /* A */ "100001001",
       /* B */ "001001001",
       /* C */ "101001000",
       /* D */ "000011001",
       /* E */ "100011000",
       /* F */ "001011000",
       /* G */ "000001101",
       /* H */ "100001100",
       /* I */ "001001100",
       /* J */ "000011100",
       /* K */ "100000011",
       /* L */ "001000011",
       /* M */ "101000010",
       /* N */ "000010011",
       /* O */ "100010010",
       /* P */ "001010010",
       /* Q */ "000000111",
       /* R */ "100000110",
       /* S */ "001000110",
       /* T */ "000010110",
       /* U */ "110000001",
       /* V */ "011000001",
       /* W */ "111000000",
       /* X */ "010010001",
       /* Y */ "110010000",
       /* Z */ "011010000",
       /* - */ "010000101",
       /* . */ "110000100",
       /*' '*/ "011000100",
       /* $ */ "010101000",
       /* / */ "010100010",
       /* + */ "010001010",
       /* % */ "000101010",
       /* * */ "010010100"
            };


            strSource = strSource.ToUpper();

            //實作圖片
            Bitmap objBitmap = new Bitmap(
              ((WidLength * 3 + NarrowLength * 7) * (intSourceLength + 2)) + (x * 2),
              BarCodeHeight + (y * 2));
            objBitmap.SetResolution(200f, 200f);

            Graphics objGraphics = Graphics.FromImage(objBitmap); //宣告GDI+繪圖介面

            //填上底色
            objGraphics.FillRectangle(Brushes.White, 0, 0, objBitmap.Width, objBitmap.Height);

            for (int i = 0; i < intSourceLength; i++)
            {

                if (AlphaBet.IndexOf(strSource[i]) == -1 || strSource[i] == '*') //檢查是否有非法字元
                {
                    objGraphics.DrawString("含有非法字元", SystemFonts.DefaultFont, Brushes.Red, x, y);
                    return objBitmap;
                }
                //查表編碼
                strEncode = string.Format("{0}0{1}", strEncode, Code39[AlphaBet.IndexOf(strSource[i])]);
            }

            strEncode = string.Format("{0}0010010100", strEncode); //補上結束符號 *

            int intEncodeLength = strEncode.Length; //編碼後長度
            int intBarWidth;

            for (int i = 0; i < intEncodeLength; i++) //依碼畫出Code39 BarCode
            {
                intBarWidth = strEncode[i] == '1' ? WidLength : NarrowLength;
                objGraphics.FillRectangle(i % 2 == 0 ? Brushes.Black : Brushes.White,
                  x, y, intBarWidth, BarCodeHeight);
                x += intBarWidth;
            }
            return objBitmap;
        }

        private static BitMatrix CutWhiteBorder(BitMatrix matrix)
        {
            int[] rec = matrix.getEnclosingRectangle();
            int resWidth = rec[2] + 1;
            int resHeight = rec[3] + 1;
            BitMatrix resMatrix = new BitMatrix(resWidth + 1, resHeight + 1);
            resMatrix.clear();
            for (int i = 0; i < resWidth; i++)
            {
                for (int j = 0; j < resHeight; j++)
                {
                    if (matrix[i + rec[0], j + rec[1]])
                    {
                        resMatrix.flip(i + 1, j + 1);
                    }
                }
            }
            return resMatrix;
        }

        static void ErrorLog(string message)
        {
            string logFile = appPath + @"\Error.log";
            string text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "\t" + message;
            using (StreamWriter fs = new StreamWriter(logFile, true, Encoding.GetEncoding("big5")))
            {
                fs.WriteLine(text);
            }
        }

        static bool LoadData(string afile)
        {
            XmlDocument xmlDOc = new XmlDocument();
            xmlDOc.Load(afile);
            int row = 0;

            XmlNodeList nodeList = xmlDOc.SelectNodes(@"data/row/col");

            //判斷節點存在
            if (nodeList == null)
            {
                ErrorLog("載入的XML檔案" + afile + ",不存在data/row/col結構! -- 終止執行");
                return false;
            }

            foreach (XmlNode oneNode in nodeList)
            {
                row++;
                if (oneNode.Attributes["name"] != null)
                {
                    string name = oneNode.Attributes["name"].Value;
                    dr[name] = oneNode.InnerText;
                }
                else
                {
                    ErrorLog("第" + row.ToString() + "個col發現沒有name屬性");
                }
            }

            if (dr["detail"] == "1")
            {
                XmlNodeList nodeList2 = xmlDOc.SelectNodes(@"detail/products");
                
                //判斷節點存在
                if (nodeList2 == null)
                {
                    ErrorLog("載入的XML檔案" + afile + ",不存在detail/products結構! -- 終止執行");
                    return false;
                }
                

                foreach (XmlNode node in nodeList2)
                {
                    string product = "";
                    for (int i = 0; i < node.ChildNodes.Count; i++)
                    {
                        string text = node.ChildNodes[i].InnerText;
                        if (i == 0)
                        {
                            product += text + "\n";
                        }
                        else
                        {
                            product += text + "\t";
                        }
                        if(i == 3)
                        {
                            products.Add(product + "\n");
                        }
                    }

                }
            }
            return true;
        }
    }

    

}
