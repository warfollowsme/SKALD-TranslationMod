using System;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace KoreanAtlasGenerator
{
    public partial class Form1 : Form
    {
        // UI 컨트롤
        private SplitContainer splitContainer; // 화면 분할용
        private Panel panelSettings; // 왼쪽 설정 패널 (SplitPanel1)
        private Panel panelPreviewArea; // 오른쪽 미리보기 패널 (SplitPanel2)
        
        private PictureBox previewBox;
        private Button btnLoadFont, btnSavePng, btnPickColor;
        private Panel pnlColorPreview;
        private NumericUpDown numCellWidth, numCellHeight, numAtlasWidth;
        private NumericUpDown numFontSize, numOffsetX, numOffsetY;
        
        private CheckBox chkGridTop, chkGridBottom, chkGridLeft, chkGridRight;
        
        private Label lblInfo;
        private Font selectedFont;
        private Color textColor = Color.Black;

        public Form1()
        {
            InitializeComponent();
            InitializeCustomUI();
            
            // 기본값 설정
            selectedFont = new Font("조선100년체", 12, FontStyle.Regular);
            numCellWidth.Value = 17;
            numCellHeight.Value = 17;
            numAtlasWidth.Value = 153;
            numFontSize.Value = 10;
            
            // 정렬 기본값
            numOffsetX.Value = -2; 
            numOffsetY.Value = 2; 

            // 그리드 기본값 (우측, 하단)
            chkGridTop.Checked = true;
            chkGridLeft.Checked = false;
            chkGridRight.Checked = true;
            chkGridBottom.Checked = false;
        }

        private void InitializeCustomUI()
        {
            this.Text = "한글 폰트 아틀라스 생성기 v3 (화면 분할 적용)";
            this.Size = new Size(1100, 850);

            // 1. SplitContainer 생성 (가장 중요: 화면을 물리적으로 분할)
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1, // 왼쪽 패널 크기 고정
                SplitterDistance = 300, // 설정창 너비
                Orientation = Orientation.Vertical
            };
            this.Controls.Add(splitContainer);

            // 2. 왼쪽 패널 (설정 영역) 구성
            panelSettings = splitContainer.Panel1;
            panelSettings.BackColor = Color.WhiteSmoke;
            panelSettings.AutoScroll = true; // 설정이 많으면 스크롤

            // 3. 오른쪽 패널 (미리보기 영역) 구성
            panelPreviewArea = splitContainer.Panel2;
            panelPreviewArea.BackColor = Color.DarkGray;
            panelPreviewArea.AutoScroll = true; // 이미지가 크면 스크롤

            // PictureBox를 오른쪽 패널에 추가
            previewBox = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.AutoSize,
                Location = new Point(0, 0),
                BackColor = Color.Transparent 
                // 배경을 투명으로 하면 Panel의 DarkGray가 보임
            };
            panelPreviewArea.Controls.Add(previewBox);

            // -------------------------------------------------------
            // UI 컨트롤 배치 (panelSettings에 추가)
            // -------------------------------------------------------
            int currentY = 10;
            const int LABEL_X = 15;
            const int CTRL_X = 15;

            void AddHeader(string text) {
                Label l = new Label { Text = text, Location = new Point(LABEL_X, currentY), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
                panelSettings.Controls.Add(l);
                currentY += 25;
            }
            void AddLabel(string text) {
                Label l = new Label { Text = text, Location = new Point(LABEL_X, currentY), AutoSize = true };
                panelSettings.Controls.Add(l);
                currentY += 20;
            }
            NumericUpDown AddNum(decimal min, decimal max, decimal val) {
                NumericUpDown n = new NumericUpDown { Minimum = min, Maximum = max, Value = val, Location = new Point(CTRL_X, currentY), Width = 140 };
                panelSettings.Controls.Add(n);
                currentY += 30;
                return n;
            }

            AddHeader("[ 1. 크기 설정 ]");
            AddLabel("아틀라스 가로 폭 (고정):");
            numAtlasWidth = AddNum(72, 8192, 512);
            AddLabel("타일 가로 (Cell Width):");
            numCellWidth = AddNum(8, 256, 32);
            AddLabel("타일 세로 (Cell Height):");
            numCellHeight = AddNum(8, 256, 32);

            currentY += 10;
            AddHeader("[ 2. 폰트 및 색상 ]");
            
            btnLoadFont = new Button { Text = "폰트 변경 (TTF)", Location = new Point(CTRL_X, currentY), Width = 200, Height = 35 };
            btnLoadFont.Click += BtnLoadFont_Click;
            panelSettings.Controls.Add(btnLoadFont);
            currentY += 40;

            AddLabel("폰트 크기:");
            numFontSize = AddNum(4, 200, 20);

            AddLabel("글자 색상:");
            btnPickColor = new Button { Text = "색상 변경", Location = new Point(CTRL_X, currentY), Width = 120, Height = 25 };
            pnlColorPreview = new Panel { Location = new Point(CTRL_X + 130, currentY), Width = 70, Height = 25, BackColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            btnPickColor.Click += BtnPickColor_Click;
            panelSettings.Controls.Add(btnPickColor);
            panelSettings.Controls.Add(pnlColorPreview);
            currentY += 35;

            currentY += 10;
            AddHeader("[ 3. 위치 미세 조정 ]");
            AddLabel("X 오프셋 (좌우):");
            numOffsetX = AddNum(-50, 50, 0);
            AddLabel("Y 오프셋 (상하):");
            numOffsetY = AddNum(-50, 50, 0);

            currentY += 10;
            AddHeader("[ 4. 그리드(보라색) 방향 ]");
            
            chkGridTop = new CheckBox { Text = "Top (상)", Location = new Point(CTRL_X, currentY), Width = 80 };
            chkGridBottom = new CheckBox { Text = "Bottom (하)", Location = new Point(CTRL_X + 90, currentY), Width = 90 };
            panelSettings.Controls.Add(chkGridTop);
            panelSettings.Controls.Add(chkGridBottom);
            currentY += 25;
            
            chkGridLeft = new CheckBox { Text = "Left (좌)", Location = new Point(CTRL_X, currentY), Width = 80 };
            chkGridRight = new CheckBox { Text = "Right (우)", Location = new Point(CTRL_X + 90, currentY), Width = 90 };
            panelSettings.Controls.Add(chkGridLeft);
            panelSettings.Controls.Add(chkGridRight);
            currentY += 35;

            // 버튼들
            currentY += 10;
            Button btnPreview = new Button { Text = "미리보기 생성 (새로고침)", Location = new Point(CTRL_X, currentY), Width = 240, Height = 50, BackColor = Color.LightBlue };
            btnPreview.Click += (s, e) => GenerateAtlas(false);
            panelSettings.Controls.Add(btnPreview);
            currentY += 60;

            btnSavePng = new Button { Text = "PNG 아틀라스 저장", Location = new Point(CTRL_X, currentY), Width = 240, Height = 50, BackColor = Color.LightGreen };
            btnSavePng.Click += (s, e) => GenerateAtlas(true);
            panelSettings.Controls.Add(btnSavePng);
            currentY += 60;

            lblInfo = new Label { Text = "준비됨.", Location = new Point(CTRL_X, currentY), AutoSize = true, ForeColor = Color.Blue };
            panelSettings.Controls.Add(lblInfo);
        }

        private void BtnPickColor_Click(object sender, EventArgs e)
        {
            ColorDialog cd = new ColorDialog();
            cd.Color = textColor;
            if (cd.ShowDialog() == DialogResult.OK)
            {
                textColor = cd.Color;
                pnlColorPreview.BackColor = textColor;
            }
        }

        private void BtnLoadFont_Click(object sender, EventArgs e)
        {
            FontDialog fd = new FontDialog();
            fd.Font = selectedFont;
            if (fd.ShowDialog() == DialogResult.OK)
            {
                selectedFont = fd.Font;
                numFontSize.Value = (decimal)selectedFont.Size;
            }
        }

        private void GenerateAtlas(bool saveMode)
        {
            try
            {
                string allChars = GetKoreanKSX1001().ToUpper();
                //string allChars = GetKoreanKSX1001();
                
                int atlasWidth = (int)numAtlasWidth.Value;
                int cellW = (int)numCellWidth.Value;
                int cellH = (int)numCellHeight.Value;
                int offsetX = (int)numOffsetX.Value;
                int offsetY = (int)numOffsetY.Value;
                float fontSize = (float)numFontSize.Value;

                int columns = atlasWidth / cellW;
                if (columns < 1) throw new Exception("타일 폭이 아틀라스 폭보다 큽니다.");
                
                int totalRows = (int)Math.Ceiling((double)allChars.Length / columns);
                int atlasHeight = totalRows * cellH;

                lblInfo.Text = $"글자: {allChars.Length}자 | 크기: {atlasWidth}x{atlasHeight} | 행: {totalRows}";

                Bitmap bmp = new Bitmap(atlasWidth, atlasHeight);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    //g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    //g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;   //good
                    g.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;            // 힌팅 안쓰는게 더 깔끔... 왜?
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    g.Clear(Color.Transparent); 

                    Font drawFont = new Font(selectedFont.FontFamily, fontSize, selectedFont.Style);
                    Brush textBrush = new SolidBrush(textColor);
                    Pen gridPen = new Pen(Color.Magenta, 1); 

                    for (int i = 0; i < allChars.Length; i++)
                    {
                        // 좌측 하단부터 시작해서 오른쪽으로, 줄 꽉 차면 위쪽 줄로
                        int col = i % columns;           
                        int rowFromBottom = i / columns; 
                        int visualRow = (totalRows - 1) - rowFromBottom;

                        int xPos = col * cellW;
                        int yPos = visualRow * cellH;

                        // 그리드 그리기
                        if (chkGridTop.Checked)    g.DrawLine(gridPen, xPos, yPos, xPos + cellW, yPos);
                        if (chkGridBottom.Checked) g.DrawLine(gridPen, xPos, yPos + cellH - 1, xPos + cellW, yPos + cellH - 1);
                        if (chkGridLeft.Checked)   g.DrawLine(gridPen, xPos, yPos, xPos, yPos + cellH);
                        if (chkGridRight.Checked)  g.DrawLine(gridPen, xPos + cellW - 1, yPos, xPos + cellW - 1, yPos + cellH);

                        // 글자 그리기
                        float fontHeight = drawFont.GetHeight(g);
                        float drawX = xPos + offsetX;
                        float drawY = yPos + (cellH - fontHeight) + offsetY;

                        g.DrawString(allChars[i].ToString(), drawFont, textBrush, drawX, drawY);
                    }
                    
                    Console.WriteLine(drawFont.FontFamily);
                }

                if (saveMode)
                {
                    SaveFileDialog sfd = new SaveFileDialog();
                    sfd.Filter = "PNG Image|*.png";
                    sfd.FileName = "KoreanAtlas_BottomUp.png";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        bmp.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
                        MessageBox.Show("저장 완료!");
                    }
                }
                else
                {
                    if (previewBox.Image != null) previewBox.Image.Dispose();
                    previewBox.Image = bmp;
                    
                    // PictureBox 크기 동기화 (필수)
                    previewBox.Size = new Size(bmp.Width, bmp.Height);
                    previewBox.Refresh();

                    // 미리보기가 너무 작으면 중앙에 예쁘게 배치 (선택사항)
                    if (bmp.Width < panelPreviewArea.Width && bmp.Height < panelPreviewArea.Height)
                    {
                        previewBox.Location = new Point(
                            (panelPreviewArea.Width - bmp.Width) / 2,
                            (panelPreviewArea.Height - bmp.Height) / 2
                        );
                    }
                    else
                    {
                        previewBox.Location = new Point(0, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("에러: " + ex.Message);
            }
        }

        private string GetKoreanKSX1001()
        {
            //const string rawData = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 0123456789.,:/%-+?'\"()=*_[]#<>;가각간갇갈갉갊감갑값갓갔강갖갗같갚갛개객갠갤갬갭갯갰갱갸갹갼걀걋걍걔걘걜거걱건걷걸걺검겁것겄겅겆겉겊겋게겐겔겜겝겟겠겡겨격겪견겯결겸겹겻겼경곁계곈곌곕곗고곡곤곧골곪곬곯곰곱곳공곶과곽관괄괆괌괍괏광괘괜괠괩괬괭괴괵괸괼굄굅굇굉교굔굘굡굣구국군굳굴굵굶굻굼굽굿궁궂궈궉권궐궜궝궤궷귀귁귄귈귐귑귓규균귤그극근귿글긁금급긋긍긔기긱긴긷길긺김깁깃깅깆깊까깍깎깐깔깖깜깝깟깠깡깥깨깩깬깰깸깹깻깼깽꺄꺅꺌꺼꺽꺾껀껄껌껍껏껐껑께껙껜껨껫껭껴껸껼꼇꼈꼍꼐꼬꼭꼰꼲꼴꼼꼽꼿꽁꽂꽃꽈꽉꽐꽜꽝꽤꽥꽹꾀꾄꾈꾐꾑꾕꾜꾸꾹꾼꿀꿇꿈꿉꿋꿍꿎꿔꿜꿨꿩꿰꿱꿴꿸뀀뀁뀄뀌뀐뀔뀜뀝뀨끄끅끈끊끌끎끓끔끕끗끙끝끼끽낀낄낌낍낏낑나낙낚난낟날낡낢남납낫났낭낮낯낱낳내낵낸낼냄냅냇냈냉냐냑냔냘냠냥너넉넋넌널넒넓넘넙넛넜넝넣네넥넨넬넴넵넷넸넹녀녁년녈념녑녔녕녘녜녠노녹논놀놂놈놉놋농높놓놔놘놜놨뇌뇐뇔뇜뇝뇟뇨뇩뇬뇰뇹뇻뇽누눅눈눋눌눔눕눗눙눠눴눼뉘뉜뉠뉨뉩뉴뉵뉼늄늅늉느늑는늘늙늚늠늡늣능늦늪늬늰늴니닉닌닐닒님닙닛닝닢다닥닦단닫달닭닮닯닳담답닷닸당닺닻닿대댁댄댈댐댑댓댔댕댜더덕덖던덛덜덞덟덤덥덧덩덫덮데덱덴델뎀뎁뎃뎄뎅뎌뎐뎔뎠뎡뎨뎬도독돈돋돌돎돐돔돕돗동돛돝돠돤돨돼됐되된될됨됩됫됬됴두둑둔둘둠둡둣둥둬뒀뒈뒝뒤뒨뒬뒵뒷뒹듀듄듈듐듕드득든듣들듦듬듭듯등듸디딕딘딛딜딤딥딧딨딩딪따딱딴딸땀땁땃땄땅땋때땍땐땔땜땝땟땠땡떠떡떤떨떪떫떰떱떳떴떵떻떼떽뗀뗄뗌뗍뗏뗐뗑뗘뗬또똑똔똘똥똬똴뙈뙤뙨뚜뚝뚠뚤뚫뚬뚱뛔뛰뛴뛸뜀뜁뜅뜨뜩뜬뜯뜰뜸뜹뜻띄띈띌띔띕띠띤띨띰띱띳띵라락란랄람랍랏랐랑랒랖랗래랙랜랠램랩랫랬랭랴략랸럇량러럭런럴럼럽럿렀렁렇레렉렌렐렘렙렛렝려력련렬렴렵렷렸령례롄롑롓로록론롤롬롭롯롱롸롼뢍뢨뢰뢴뢸룀룁룃룅료룐룔룝룟룡루룩룬룰룸룹룻룽뤄뤘뤠뤼뤽륀륄륌륏륑류륙륜률륨륩륫륭르륵른를름릅릇릉릊릍릎리릭린릴림립릿링마막만많맏말맑맒맘맙맛망맞맡맣매맥맨맬맴맵맷맸맹맺먀먁먈먕머먹먼멀멂멈멉멋멍멎멓메멕멘멜멤멥멧멨멩며멱면멸몃몄명몇몌모목몫몬몰몲몸몹못몽뫄뫈뫘뫙뫼묀묄묍묏묑묘묜묠묩묫무묵묶문묻물묽묾뭄뭅뭇뭉뭍뭏뭐뭔뭘뭡뭣뭬뮈뮌뮐뮤뮨뮬뮴뮷므믄믈믐믓미믹민믿밀밂밈밉밋밌밍및밑바박밖밗반받발밝밞밟밤밥밧방밭배백밴밸뱀뱁뱃뱄뱅뱉뱌뱍뱐뱝버벅번벋벌벎범법벗벙벚베벡벤벧벨벰벱벳벴벵벼벽변별볍볏볐병볕볘볜보복볶본볼봄봅봇봉봐봔봤봬뵀뵈뵉뵌뵐뵘뵙뵤뵨부북분붇불붉붊붐붑붓붕붙붚붜붤붰붸뷔뷕뷘뷜뷩뷰뷴뷸븀븃븅브븍븐블븜븝븟비빅빈빌빎빔빕빗빙빚빛빠빡빤빨빪빰빱빳빴빵빻빼빽뺀뺄뺌뺍뺏뺐뺑뺘뺙뺨뻐뻑뻔뻗뻘뻠뻣뻤뻥뻬뼁뼈뼉뼘뼙뼛뼜뼝뽀뽁뽄뽈뽐뽑뽕뾔뾰뿅뿌뿍뿐뿔뿜뿟뿡쀼쁑쁘쁜쁠쁨쁩삐삑삔삘삠삡삣삥사삭삯산삳살삵삶삼삽삿샀상샅새색샌샐샘샙샛샜생샤샥샨샬샴샵샷샹섀섄섈섐섕서석섞섟선섣설섦섧섬섭섯섰성섶세섹센셀셈셉셋셌셍셔셕션셜셤셥셧셨셩셰셴셸솅소속솎손솔솖솜솝솟송솥솨솩솬솰솽쇄쇈쇌쇔쇗쇘쇠쇤쇨쇰쇱쇳쇼쇽숀숄숌숍숏숑수숙순숟술숨숩숫숭숯숱숲숴쉈쉐쉑쉔쉘쉠쉥쉬쉭쉰쉴쉼쉽쉿슁슈슉슐슘슛슝스슥슨슬슭슴습슷승시식신싣실싫심십싯싱싶싸싹싻싼쌀쌈쌉쌌쌍쌓쌔쌕쌘쌜쌤쌥쌨쌩썅써썩썬썰썲썸썹썼썽쎄쎈쎌쏀쏘쏙쏜쏟쏠쏢쏨쏩쏭쏴쏵쏸쐈쐐쐤쐬쐰쐴쐼쐽쑈쑤쑥쑨쑬쑴쑵쑹쒀쒔쒜쒸쒼쓩쓰쓱쓴쓸쓺쓿씀씁씌씐씔씜씨씩씬씰씸씹씻씽아악안앉않알앍앎앓암압앗았앙앝앞애액앤앨앰앱앳앴앵야약얀얄얇얌얍얏양얕얗얘얜얠얩어억언얹얻얼얽얾엄업없엇었엉엊엌엎에엑엔엘엠엡엣엥여역엮연열엶엷염엽엾엿였영옅옆옇예옌옐옘옙옛옜오옥온올옭옮옰옳옴옵옷옹옻와왁완왈왐왑왓왔왕왜왝왠왬왯왱외왹왼욀욈욉욋욍요욕욘욜욤욥욧용우욱운울욹욺움웁웃웅워웍원월웜웝웠웡웨웩웬웰웸웹웽위윅윈윌윔윕윗윙유육윤율윰윱윳융윷으윽은을읊음읍읏응읒읓읔읕읖읗의읜읠읨읫이익인일읽읾잃임입잇있잉잊잎자작잔잖잗잘잚잠잡잣잤장잦재잭잰잴잼잽잿쟀쟁쟈쟉쟌쟎쟐쟘쟝쟤쟨쟬저적전절젊점접젓정젖제젝젠젤젬젭젯젱져젼졀졈졉졌졍졔조족존졸졺좀좁좃종좆좇좋좌좍좔좝좟좡좨좼좽죄죈죌죔죕죗죙죠죡죤죵주죽준줄줅줆줌줍줏중줘줬줴쥐쥑쥔쥘쥠쥡쥣쥬쥰쥴쥼즈즉즌즐즘즙즛증지직진짇질짊짐집짓징짖짙짚짜짝짠짢짤짧짬짭짯짰짱째짹짼쨀쨈쨉쨋쨌쨍쨔쨘쨩쩌쩍쩐쩔쩜쩝쩟쩠쩡쩨쩽쪄쪘쪼쪽쫀쫄쫌쫍쫏쫑쫓쫘쫙쫠쫬쫴쬈쬐쬔쬘쬠쬡쭁쭈쭉쭌쭐쭘쭙쭝쭤쭸쭹쮜쮸쯔쯤쯧쯩찌찍찐찔찜찝찡찢찧차착찬찮찰참찹찻찼창찾채책챈챌챔챕챗챘챙챠챤챦챨챰챵처척천철첨첩첫첬청체첵첸첼쳄쳅쳇쳉쳐쳔쳤쳬쳰촁초촉촌촐촘촙촛총촤촨촬촹최쵠쵤쵬쵭쵯쵱쵸춈추축춘출춤춥춧충춰췄췌췐취췬췰췸췹췻췽츄츈츌츔츙츠측츤츨츰츱츳층치칙친칟칠칡침칩칫칭카칵칸칼캄캅캇캉캐캑캔캘캠캡캣캤캥캬캭컁커컥컨컫컬컴컵컷컸컹케켁켄켈켐켑켓켕켜켠켤켬켭켯켰켱켸코콕콘콜콤콥콧콩콰콱콴콸쾀쾅쾌쾡쾨쾰쿄쿠쿡쿤쿨쿰쿱쿳쿵쿼퀀퀄퀑퀘퀭퀴퀵퀸퀼큄큅큇큉큐큔큘큠크큭큰클큼큽킁키킥킨킬킴킵킷킹타탁탄탈탉탐탑탓탔탕태택탠탤탬탭탯탰탱탸턍터턱턴털턺텀텁텃텄텅테텍텐텔템텝텟텡텨텬텼톄톈토톡톤톨톰톱톳통톺톼퇀퇘퇴퇸툇툉툐투툭툰툴툼툽툿퉁퉈퉜퉤튀튁튄튈튐튑튕튜튠튤튬튱트특튼튿틀틂틈틉틋틔틘틜틤틥티틱틴틸팀팁팃팅파팍팎판팔팖팜팝팟팠팡팥패팩팬팰팸팹팻팼팽퍄퍅퍼퍽펀펄펌펍펏펐펑페펙펜펠펨펩펫펭펴편펼폄폅폈평폐폘폡폣포폭폰폴폼폽폿퐁퐈퐝푀푄표푠푤푭푯푸푹푼푿풀풂품풉풋풍풔풩퓌퓐퓔퓜퓟퓨퓬퓰퓸퓻퓽프픈플픔픕픗피픽핀필핌핍핏핑하학한할핥함합핫항핳해핵핸핼햄햅햇했행햐향허헉헌헐헒험헙헛헝헤헥헨헬헴헵헷헹혀혁현혈혐협혓혔형혜혠혤혭호혹혼홀홅홈홉홋홍홑화확환활홧황홰홱홴횃횅회획횐횔횝횟횡효횬횰횹횻후훅훈훌훑훔훗훙훠훤훨훰훵훼훽휀휄휑휘휙휜휠휨휩휫휭휴휵휸휼흄흇흉흐흑흔흖흗흘흙흠흡흣흥흩희흰흴흼흽힁히힉힌힐힘힙힛힝힣ㄱㄴㄷㄹㅁㅂㅅㅇㅈㅊㅋㅌㅍㅎㄲㄸㅃㅆㅉㄺㅀㄻㄼㅄㄳㄶㄵㄽㅏㅑㅓㅕㅗㅛㅜㅠㅡㅣㅒㅖ";
            const string rawData =
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz!0123456789.,:/%-+?'\"()=*_[]#<>;{}|  가각간갇갈갉갊감갑값갓갔강갖갗같갚갛개객갠갤갬갭갯갰갱갸갹갼걀걋걍걔걘걜거걱건걷걸걺검겁것겄겅겆겉겊겋게겐겔겜겝겟겠겡겨격겪견겯결겸겹겻겼경곁계곈곌곕곗고곡곤곧골곪곬곯곰곱곳공곶과곽관괄괆괌괍괏광괘괜괠괩괬괭괴괵괸괼굄굅굇굉교굔굘굡굣구국군굳굴굵굶굻굼굽굿궁궂궈궉권궐궜궝궤궷귀귁귄귈귐귑귓규균귤그극근귿글긁금급긋긍긔기긱긴긷길긺김깁깃깅깆깊까깍깎깐깔깖깜깝깟깠깡깥깨깩깬깰깸깹깻깼깽꺄꺅꺌꺼꺽꺾껀껄껌껍껏껐껑께껙껜껨껫껭껴껸껼꼇꼈꼍꼐꼬꼭꼰꼲꼴꼼꼽꼿꽁꽂꽃꽈꽉꽐꽜꽝꽤꽥꽹꾀꾄꾈꾐꾑꾕꾜꾸꾹꾼꿀꿇꿈꿉꿋꿍꿎꿔꿜꿨꿩꿰꿱꿴꿸뀀뀁뀄뀌뀐뀔뀜뀝뀨끄끅끈끊끌끎끓끔끕끗끙끝끼끽낀낄낌낍낏낑나낙낚난낟날낡낢남납낫났낭낮낯낱낳내낵낸낼냄냅냇냈냉냐냑냔냘냠냥너넉넋넌널넒넓넘넙넛넜넝넣네넥넨넬넴넵넷넸넹녀녁년녈념녑녔녕녘녜녠노녹논놀놂놈놉놋농높놓놔놘놜놨뇌뇐뇔뇜뇝뇟뇨뇩뇬뇰뇹뇻뇽누눅눈눋눌눔눕눗눙눠눴눼뉘뉜뉠뉨뉩뉴뉵뉼늄늅늉느늑는늘늙늚늠늡늣능늦늪늬늰늴니닉닌닐닒님닙닛닝닢다닥닦단닫달닭닮닯닳담답닷닸당닺닻닿대댁댄댈댐댑댓댔댕댜더덕덖던덛덜덞덟덤덥덧덩덫덮데덱덴델뎀뎁뎃뎄뎅뎌뎐뎔뎠뎡뎨뎬도독돈돋돌돎돐돔돕돗동돛돝돠돤돨돼됐되된될됨됩됫됬됴두둑둔둘둠둡둣둥둬뒀뒈뒝뒤뒨뒬뒵뒷뒹듀듄듈듐듕드득든듣들듦듬듭듯등듸디딕딘딛딜딤딥딧딨딩딪따딱딴딸땀땁땃땄땅땋때땍땐땔땜땝땟땠땡떠떡떤떨떪떫떰떱떳떴떵떻떼떽뗀뗄뗌뗍뗏뗐뗑뗘뗬또똑똔똘똥똬똴뙈뙤뙨뚜뚝뚠뚤뚫뚬뚱뛔뛰뛴뛸뜀뜁뜅뜨뜩뜬뜯뜰뜸뜹뜻띄띈띌띔띕띠띤띨띰띱띳띵라락란랄람랍랏랐랑랒랖랗래랙랜랠램랩랫랬랭랴략랸럇량러럭런럴럼럽럿렀렁렇레렉렌렐렘렙렛렝려력련렬렴렵렷렸령례롄롑롓로록론롤롬롭롯롱롸롼뢍뢨뢰뢴뢸룀룁룃룅료룐룔룝룟룡루룩룬룰룸룹룻룽뤄뤘뤠뤼뤽륀륄륌륏륑류륙륜률륨륩륫륭르륵른를름릅릇릉릊릍릎리릭린릴림립릿링마막만많맏말맑맒맘맙맛망맞맡맣매맥맨맬맴맵맷맸맹맺먀먁먈먕머먹먼멀멂멈멉멋멍멎멓메멕멘멜멤멥멧멨멩며멱면멸몃몄명몇몌모목몫몬몰몲몸몹못몽뫄뫈뫘뫙뫼묀묄묍묏묑묘묜묠묩묫무묵묶문묻물묽묾뭄뭅뭇뭉뭍뭏뭐뭔뭘뭡뭣뭬뮈뮌뮐뮤뮨뮬뮴뮷므믄믈믐믓미믹민믿밀밂밈밉밋밌밍및밑바박밖밗반받발밝밞밟밤밥밧방밭배백밴밸뱀뱁뱃뱄뱅뱉뱌뱍뱐뱝버벅번벋벌벎범법벗벙벚베벡벤벧벨벰벱벳벴벵벼벽변별볍볏볐병볕볘볜보복볶본볼봄봅봇봉봐봔봤봬뵀뵈뵉뵌뵐뵘뵙뵤뵨부북분붇불붉붊붐붑붓붕붙붚붜붤붰붸뷔뷕뷘뷜뷩뷰뷴뷸븀븃븅브븍븐블븜븝븟비빅빈빌빎빔빕빗빙빚빛빠빡빤빨빪빰빱빳빴빵빻빼빽뺀뺄뺌뺍뺏뺐뺑뺘뺙뺨뻐뻑뻔뻗뻘뻠뻣뻤뻥뻬뼁뼈뼉뼘뼙뼛뼜뼝뽀뽁뽄뽈뽐뽑뽕뾔뾰뿅뿌뿍뿐뿔뿜뿟뿡쀼쁑쁘쁜쁠쁨쁩삐삑삔삘삠삡삣삥사삭삯산삳살삵삶삼삽삿샀상샅새색샌샐샘샙샛샜생샤샥샨샬샴샵샷샹섀섄섈섐섕서석섞섟선섣설섦섧섬섭섯섰성섶세섹센셀셈셉셋셌셍셔셕션셜셤셥셧셨셩셰셴셸솅소속솎손솔솖솜솝솟송솥솨솩솬솰솽쇄쇈쇌쇔쇗쇘쇠쇤쇨쇰쇱쇳쇼쇽숀숄숌숍숏숑수숙순숟술숨숩숫숭숯숱숲숴쉈쉐쉑쉔쉘쉠쉥쉬쉭쉰쉴쉼쉽쉿슁슈슉슐슘슛슝스슥슨슬슭슴습슷승시식신싣실싫심십싯싱싶싸싹싻싼쌀쌈쌉쌌쌍쌓쌔쌕쌘쌜쌤쌥쌨쌩썅써썩썬썰썲썸썹썼썽쎄쎈쎌쏀쏘쏙쏜쏟쏠쏢쏨쏩쏭쏴쏵쏸쐈쐐쐤쐬쐰쐴쐼쐽쑈쑤쑥쑨쑬쑴쑵쑹쒀쒔쒜쒸쒼쓩쓰쓱쓴쓸쓺쓿씀씁씌씐씔씜씨씩씬씰씸씹씻씽아악안앉않알앍앎앓암압앗았앙앝앞애액앤앨앰앱앳앴앵야약얀얄얇얌얍얏양얕얗얘얜얠얩어억언얹얻얼얽얾엄업없엇었엉엊엌엎에엑엔엘엠엡엣엥여역엮연열엶엷염엽엾엿였영옅옆옇예옌옐옘옙옛옜오옥온올옭옮옰옳옴옵옷옹옻와왁완왈왐왑왓왔왕왜왝왠왬왯왱외왹왼욀욈욉욋욍요욕욘욜욤욥욧용우욱운울욹욺움웁웃웅워웍원월웜웝웠웡웨웩웬웰웸웹웽위윅윈윌윔윕윗윙유육윤율윰윱윳융윷으윽은을읊음읍읏응읒읓읔읕읖읗의읜읠읨읫이익인일읽읾잃임입잇있잉잊잎자작잔잖잗잘잚잠잡잣잤장잦재잭잰잴잼잽잿쟀쟁쟈쟉쟌쟎쟐쟘쟝쟤쟨쟬저적전절젊점접젓정젖제젝젠젤젬젭젯젱져젼졀졈졉졌졍졔조족존졸졺좀좁좃종좆좇좋좌좍좔좝좟좡좨좼좽죄죈죌죔죕죗죙죠죡죤죵주죽준줄줅줆줌줍줏중줘줬줴쥐쥑쥔쥘쥠쥡쥣쥬쥰쥴쥼즈즉즌즐즘즙즛증지직진짇질짊짐집짓징짖짙짚짜짝짠짢짤짧짬짭짯짰짱째짹짼쨀쨈쨉쨋쨌쨍쨔쨘쨩쩌쩍쩐쩔쩜쩝쩟쩠쩡쩨쩽쪄쪘쪼쪽쫀쫄쫌쫍쫏쫑쫓쫘쫙쫠쫬쫴쬈쬐쬔쬘쬠쬡쭁쭈쭉쭌쭐쭘쭙쭝쭤쭸쭹쮜쮸쯔쯤쯧쯩찌찍찐찔찜찝찡찢찧차착찬찮찰참찹찻찼창찾채책챈챌챔챕챗챘챙챠챤챦챨챰챵처척천철첨첩첫첬청체첵첸첼쳄쳅쳇쳉쳐쳔쳤쳬쳰촁초촉촌촐촘촙촛총촤촨촬촹최쵠쵤쵬쵭쵯쵱쵸춈추축춘출춤춥춧충춰췄췌췐취췬췰췸췹췻췽츄츈츌츔츙츠측츤츨츰츱츳층치칙친칟칠칡침칩칫칭카칵칸칼캄캅캇캉캐캑캔캘캠캡캣캤캥캬캭컁커컥컨컫컬컴컵컷컸컹케켁켄켈켐켑켓켕켜켠켤켬켭켯켰켱켸코콕콘콜콤콥콧콩콰콱콴콸쾀쾅쾌쾡쾨쾰쿄쿠쿡쿤쿨쿰쿱쿳쿵쿼퀀퀄퀑퀘퀭퀴퀵퀸퀼큄큅큇큉큐큔큘큠크큭큰클큼큽킁키킥킨킬킴킵킷킹타탁탄탈탉탐탑탓탔탕태택탠탤탬탭탯탰탱탸턍터턱턴털턺텀텁텃텄텅테텍텐텔템텝텟텡텨텬텼톄톈토톡톤톨톰톱톳통톺톼퇀퇘퇴퇸툇툉툐투툭툰툴툼툽툿퉁퉈퉜퉤튀튁튄튈튐튑튕튜튠튤튬튱트특튼튿틀틂틈틉틋틔틘틜틤틥티틱틴틸팀팁팃팅파팍팎판팔팖팜팝팟팠팡팥패팩팬팰팸팹팻팼팽퍄퍅퍼퍽펀펄펌펍펏펐펑페펙펜펠펨펩펫펭펴편펼폄폅폈평폐폘폡폣포폭폰폴폼폽폿퐁퐈퐝푀푄표푠푤푭푯푸푹푼푿풀풂품풉풋풍풔풩퓌퓐퓔퓜퓟퓨퓬퓰퓸퓻퓽프픈플픔픕픗피픽핀필핌핍핏핑하학한할핥함합핫항핳해핵핸핼햄햅햇했행햐향허헉헌헐헒험헙헛헝헤헥헨헬헴헵헷헹혀혁현혈혐협혓혔형혜혠혤혭호혹혼홀홅홈홉홋홍홑화확환활홧황홰홱홴횃횅회획횐횔횝횟횡효횬횰횹횻후훅훈훌훑훔훗훙훠훤훨훰훵훼훽휀휄휑휘휙휜휠휨휩휫휭휴휵휸휼흄흇흉흐흑흔흖흗흘흙흠흡흣흥흩희흰흴흼흽힁히힉힌힐힘힙힛힝힣ㄱㄴㄷㄹㅁㅂㅅㅇㅈㅊㅋㅌㅍㅎㄲㄸㅃㅆㅉㄺㅀㄻㄼㅄㄳㄶㄵㄽㅏㅑㅓㅕㅗㅛㅜㅠㅡㅣㅒㅖ";
            
            jsonPrinter(rawData);
            return rawData;
            
            StringBuilder sb = new StringBuilder();
            char start = '\uAC00';
            for (int i = 0; i < 2350; i++) sb.Append((char)(start + i));
            return sb.ToString();
        }

        private void jsonPrinter(string input)
        {
            var characterChart = new Dictionary<string, int>();

            int index = 0;
            foreach (char c in input)
            {
                characterChart[c.ToString()] = index++;
            }

            var result = new
            {
                languageCode = "ko",
                name = "Korean",
                description = "한글 언어팩",
                version = "1.0.0",
                fontFilesPath = "fonts",
                translationFilesPath = "translations",
                characterChart = characterChart
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            
            Console.OutputEncoding = Encoding.UTF8;

            string json = JsonSerializer.Serialize(result, options);
            Console.WriteLine(json);
        }

        // WinForms 필수 초기화
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Text = "Form1";
        }
    }
}