// Rebuild the self-contained handoff document: node docs/tools/build_handoff_html.js
// Uses Node built-ins only. Diagram geometry and text are kept here for maintenance.
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const root = path.resolve(__dirname, '../..');
const baseline = execFileSync('git', ['rev-parse', '--short', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim();
const verifiedDate = '2026-11-03';
const out = path.join(root, 'docs/PROJECT_HANDOFF.html');
const esc = value => String(value).replace(/[&<>"']/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' }[c]));
function text(x, y, lines, cls = '', anchor = 'middle') {
  return `<text x="${x}" y="${y}" text-anchor="${anchor}" class="${cls}">${[].concat(lines).map((line, i) => `<tspan x="${x}" dy="${i ? 23 : 0}">${esc(line)}</tspan>`).join('')}</text>`;
}
function box(x,y,w,h,title,sub=[],kind='normal') {
  sub = [].concat(sub);
  const ty = y + (h - (sub.length * 23 + 21)) / 2 + 17;
  return `<g class="node ${kind}"><rect x="${x}" y="${y}" width="${w}" height="${h}" rx="12"/>${text(x+w/2,ty,title,'node-title')}${sub.length ? text(x+w/2,ty+25,sub,'node-sub') : ''}</g>`;
}
function edge(points, label='', lx, ly, dashed=false) {
  return `<polyline points="${points}" class="edge${dashed ? ' dashed' : ''}" marker-end="url(#arrow)"/>${label ? text(lx,ly,label,'edge-label') : ''}`;
}
function panel(x,y,w,h,title) {
  return `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="16" class="boundary"/>${text(x+20,y+30,title,'boundary-title','start')}`;
}
function note(x,y,w,lines) {
  return `<rect x="${x}" y="${y}" width="${w}" height="${[].concat(lines).length*23+25}" rx="8" class="note"/>${text(x+15,y+24,lines,'note-text','start')}`;
}
function diamond(cx,cy,w,h,lines) {
  return `<g class="node decision"><polygon points="${cx},${cy-h/2} ${cx+w/2},${cy} ${cx},${cy+h/2} ${cx-w/2},${cy}"/>${text(cx,cy-([].concat(lines).length-1)*10+5,lines,'node-title')}</g>`;
}
const svgStyle = `
svg{font-family:"Segoe UI","Microsoft JhengHei",sans-serif;color:#20354c}svg text{fill:#20354c;font-size:16px}.node rect,.node polygon{fill:#fff;stroke:#bacbd8;stroke-width:1.4}.node-title{font-weight:650}.node-sub{fill:#536b80;font-size:14px}.node.primary rect{fill:#eaf3ff;stroke:#6d9ed2}.node.hardware rect{fill:#e8f6f1;stroke:#7eaf9e}.node.store rect{fill:#f2edfb;stroke:#aa96c5}.node.warning rect,.node.decision polygon{fill:#fff6e4;stroke:#c6a360}.boundary{fill:#f5f8fb;stroke:#d1dce5;stroke-width:1.2}.boundary-title{font-size:14px;font-weight:650;fill:#536b80}.edge{fill:none;stroke:#5b7790;stroke-width:1.6;stroke-linejoin:round}.dashed{stroke-dasharray:6 5}.edge-label{font-size:13px;fill:#455f77;paint-order:stroke;stroke:#fff;stroke-width:5px;stroke-linejoin:round}.note{fill:#fff5dd;stroke:#ead7aa}.note-text{font-size:14px;fill:#715722}.lifeline{stroke:#bdcbd6;stroke-width:1.3;stroke-dasharray:5 5}.phase{fill:#eaf3ff;opacity:.65}.phase-title{font-size:13px;font-weight:650;fill:#466785}.activation{fill:#d8e9fb;stroke:#86a9cc}.time-label{font-size:13px;fill:#5f778b}
`;
function svg(id,w,h,title,desc,body) {
  // Marker IDs must be unique when several SVGs are embedded in the same document.
  return `<svg xmlns="http://www.w3.org/2000/svg" id="diagram-${id}" viewBox="0 0 ${w} ${h}" width="${w}" height="${h}" role="img" aria-labelledby="${id}-title ${id}-desc"><title id="${id}-title">${esc(title)}</title><desc id="${id}-desc">${esc(desc)}</desc><defs><marker id="arrow-${id}" markerWidth="10" markerHeight="8" refX="8" refY="4" orient="auto" markerUnits="userSpaceOnUse"><path d="M0,0 L8,4 L0,8" fill="#5b7790"/></marker></defs><style>${svgStyle}</style>${body.replaceAll('url(#arrow)',`url(#arrow-${id})`)}</svg>`;
}

// 01: current runtime responsibilities, not a proposed service architecture.
let a = panel(20,20,1060,178,'外部輸入與操作')
  + box(45,76,232,84,'線掃描相機 / 米輪',['Sapera / LSI8181'],'hardware')
  + box(324,76,222,84,'CameraCaptureModule',['CameraService / 取像 UI'],'hardware')
  + box(600,76,200,84,'手動圖片 / 歷史圖片',['檔案載入與重跑'])
  + box(840,76,215,84,'操作介面 / Recipe',['參數、角色與設定'])
  + edge('277,118 324,118')
  + panel(20,244,1060,331,'')
  + text(1055,274,'主程式 · MainForm 的 partial 共用同一個類別','boundary-title','end')
  + box(45,306,255,92,'CameraCaptureHostControl',['影格事件與轉送'])
  + box(353,306,355,92,'MainForm 檢測協調',['結果確認、總覽、影像生命週期'],'primary')
  + box(760,306,295,92,'設定 / 儲存 / 通訊服務',['INI、Recipe、SQLite、TCP'])
  + edge('435,160 435,220 173,220 173,306','Mono8 記憶體影格',286,212)
  + edge('700,160 700,224 530,224 530,306')
  + edge('948,160 948,244')
  + edge('300,352 353,352')
  + edge('708,352 760,352')
  + box(45,459,255,78,'找物件 / MASK',['前處理、定義與格位'])
  + box(353,459,355,78,'尺寸 / 平場 / 缺陷分析',['七個一般核心 + 頻域一、二'],'primary')
  + box(760,459,295,78,'結果 / 總覽 / 歷史',['逐物件判定與 TCP 狀態'])
  + edge('530,398 530,427 173,427 173,459')
  + edge('300,498 353,498')
  + edge('708,498 760,498')
  + note(20,603,1060,['閱讀重點：圖中的區塊是責任分區，並非獨立程序或已拆出的檢測引擎。','相機影格直接進入記憶體檢測；背景 TIFF 保存另外執行。']);
const architecture = svg('architecture',1100,685,'目前系統架構圖','兩個專案：相機類別庫向主程式轉送記憶體影格；MainForm 協調物件、尺寸、缺陷及結果，並呼叫設定、歷史及通訊服務。',a);

// 02: representative local-camera path; actual work remains within MainForm.
const xs=[100,310,535,760,1000];
let s = xs.map((x,i)=>box(x-87,20,174,66,['相機模組','Host / MainForm','檢測工作','背景存圖','歷史 / 狀態'][i],[],i===0?'hardware':i===3?'store':'normal')).join('');
s += xs.map(x=>`<line x1="${x}" y1="86" x2="${x}" y2="766" class="lifeline"/>`).join('');
s += `<rect x="20" y="180" width="1060" height="68" rx="8" class="phase"/>${text(36,202,'alt：忙碌時直接略過新影格','phase-title','start')}`;
s += edge('100,126 310,126','① ImageAcquired · Mono8 buffer',205,116);
s += `<rect x="303" y="130" width="14" height="600" class="activation"/>`;
s += edge('317,215 355,215 355,234 317,234','已有影格在途 → 略過，不排隊',550,226);
s += edge('317,286 355,286 355,310 317,310','② 可處理：建立 LargeImageSource',548,292);
s += edge('317,355 760,355','③ AddReference → 啟動背景 TIFF 保存',540,344);
s += `<rect x="753" y="355" width="14" height="300" class="activation"/>`;
s += edge('317,411 535,411','④ 呼叫結果確認流程',430,400);
s += `<rect x="528" y="411" width="14" height="158" class="activation"/>`;
s += note(365,443,330,['找物件 / MASK / 平場來源','尺寸與缺陷可並行；頻域遵守相依']);
s += edge('528,565 317,565','⑤ 回傳結果 / 快照',426,554,true);
s += edge('317,610 1000,610','⑥ 更新總覽、記錄歷史、發布最新站點狀態',565,598);
s += edge('767,655 801,655 801,681 767,681','存檔完成或失敗後 ReleaseReference',921,709);
s += edge('317,735 355,735 355,756 317,756','⑦ finally 解除 in-flight，允許後續影格',551,753);
s += note(20,794,1060,['時序為代表性成功路徑；缺少參數或相機高度不符時，可保留影像並產生待確認紀錄。','背景存圖與檢測沒有完成先後保證；圖中存檔結束位置僅為示意，檢測不等待 TIFF 落盤。']);
const sequence=svg('sequence',1100,880,'相機影格檢測循序圖','由相機事件開始，忙碌時略過，建立記憶體來源，同時啟動背景存圖與檢測，再更新結果及歷史，最後釋放參照與忙碌旗標。各泳道代表邏輯責任。',s);

// 03: high-level common processing flow, with camera-only validation labeled.
let f=box(390,20,320,56,'取得影像：手動 / 相機 / 歷史',[],'primary')
 + edge('550,76 550,103')
 + diamond(550,155,370,104,['輸入與參數','可執行？'])
 + edge('735,155 930,155 930,219','否',837,145)
 + box(810,219,260,105,'提示 / 待確認',['相機路徑可保存影像與歷史','手動拒收不保證留下紀錄'],'warning')
 + edge('550,207 550,237','是',573,227)
 + box(390,237,320,70,'找物件、排列與來源 MASK',['依目前作用中 Recipe'])
 + edge('550,307 550,334')
 + box(390,334,320,70,'準備平場校正與檢測來源',['尺寸使用其指定的 MASK'])
 + edge('550,404 550,430')
 + `<line x1="170" y1="430" x2="900" y2="430" stroke="#5b7790" stroke-width="4"/>`
 + edge('200,430 200,465') + edge('550,430 550,465') + edge('900,430 900,465')
 + box(45,465,310,100,'尺寸工作',['量測、A / B 條件','依物件可平行'],'primary')
 + box(395,465,310,100,'一般缺陷工作',['七核心：平場 + 條件一至六','ROI、門檻、形態與篩選'],'primary')
 + box(745,465,310,100,'頻域一 / 二（按需）',['加窗能量 / 可選鄰格比較','名稱沿用 UI；目前無 FFT'],'primary')
 + edge('705,524 745,524','相依',725,511,true)
 + edge('200,565 200,616') + edge('550,565 550,616') + edge('900,565 900,616')
 + `<line x1="170" y1="616" x2="930" y2="616" stroke="#5b7790" stroke-width="4"/>`
 + text(550,646,'鄰格比較的最終參考格排除，須等待一般缺陷結果','time-label')
 + edge('550,655 550,684')
 + box(390,684,320,78,'整合與逐物件綜合判定',['A / B / NG / 待確認'])
 + edge('550,762 550,792')
 + box(330,792,440,78,'結果快照 → 顯示 / 歷史 / TCP',['各入口依設定決定是否記錄歷史'],'primary')
 + note(20,900,1060,['輸入規則依入口不同：相機入口檢查全域 Y 高度；手動確認另檢查參數參考寬高。','上方分支表示可並行的工作；實際是否並行與是否執行頻域，依目前參數與啟用選項決定。']);
const flow=svg('flow',1100,986,'檢測處理流程圖','先驗證輸入及參數，建立物件與來源，準備平場，並行執行尺寸和缺陷及按需頻域，再整合逐物件結果。不可執行時提示或標記待確認。',f);

// 04: data boundaries and replay linkage, not an invented database schema.
let d=panel(20,20,1060,171,'設定來源 · 相對於執行檔目錄')
 + box(45,72,300,86,'取像與米輪設定',['captureSetting.ini'],'store')
 + box(390,72,310,86,'Recipe 與工作參數',['SystemParameters.ini','recipe/parameterList.json / {ID}.ini'],'store')
 + box(745,72,310,86,'設備 / 站點 / 標定',['config/device-settings.ini','connection-controller-settings.xml'],'store')
 + box(45,262,300,86,'影像來源',['Mono8 buffer / 手動圖片'],'hardware')
 + box(390,262,310,86,'目前檢測工作',['影像參照、作用中參數、結果'],'primary')
 + box(745,262,310,86,'總覽與站點狀態',['TCP 傳 JSON，不傳影像'])
 + edge('195,158 195,262','取像設定',195,218)
 + edge('545,158 545,262')
 + edge('900,158 900,218 650,218 650,262','設備標定 / 站點設定',800,206)
 + edge('345,305 390,305') + edge('700,305 745,305')
 + panel(20,405,1060,195,'持久化資料')
 + box(45,460,300,103,'原圖 / 圖片副本',['CameraCapture/yyyyMMdd/*.tif','snapshot/yyyyMMdd'],'store')
 + box(390,460,310,103,'每日 SQLite',['history/yyyyMMdd.db','批次、物件與尺寸 / 缺陷明細'],'store')
 + box(745,460,310,103,'歷史參數快照',['history/recipe-snapshots/{hash}.ini','SHA-256 去重'],'store')
 + edge('195,348 195,460','背景 TIFF / 按需副本',195,385)
 + edge('545,348 545,460','依入口與設定記錄',545,385)
 + edge('650,348 650,378 900,378 900,460','保存參數快照',897,402)
 + edge('390,525 345,525','路徑',367,514)
 + edge('700,525 745,525','路徑',722,514)
 + box(323,649,454,80,'歷史重跑',['原圖 + 當時快照 + ParameterId'])
 + edge('195,563 195,689 323,689')
 + edge('545,563 545,649')
 + edge('900,563 900,689 777,689')
 + note(20,760,1060,['歷史未保存當時全域 X/Y 精度；改標定後重跑的 mm 數值可能改變。','DB 與存圖不是同一交易。缺圖、缺快照或舊多參數紀錄缺 ParameterId 時，重跑可能拒絕。']);
const data=svg('data',1100,846,'資料流與保存圖','設定和影像進入檢測工作，結果更新總覽及 TCP。圖片、每日 SQLite 和去重參數快照分別保存。重跑需要圖片、當時快照及參數識別碼，且歷史尚未記錄當時設備精度。',d);

// 05: physical process boundaries; controller role is the same executable.
let p=panel(20,20,325,520,'檢測工作站 · Windows x64')
 + box(45,80,275,100,'主檢測程式 / WinForms',['IntegratedImageProcessingApp.exe','含總覽、檢測、歷史與 listener'],'primary')
 + box(45,225,275,78,'CameraCaptureModule.dll',['同一程序中的取像類別庫'],'hardware')
 + box(45,349,275,78,'SDK / 原生相依',['Sapera / OpenCV / LSI8181_64'])
 + box(45,461,275,55,'本機檔案 / SQLite',[],'store')
 + edge('183,180 183,225') + edge('183,303 183,349') + edge('85,180 30,180 30,489 45,489')
 + box(399,266,260,110,'線掃描相機 / 米輪',['Sapera 相機擷取','LSI 編碼器 / compare 輸出'],'hardware')
 + edge('320,379 366,379 366,321 399,321')
 + panel(713,20,367,520,'總控電腦 · 同套程式的總監控角色')
 + box(740,80,313,100,'總監控角色 / WinForms',['IntegratedImageProcessingApp.exe','站點卡片 / 多站管理'],'primary')
 + box(740,225,313,78,'總控站點設定',['IP、Port、站點 ID、心跳 / 重連'],'store')
 + box(740,349,313,103,'其他被控站 × N',['每站獨立程式、相機與資料','站點識別與連線需逐一確認'])
 + edge('896,225 896,180') + edge('896,180 1065,180 1065,400 1053,400','多站連線',1020,323)
 + edge('740,115 320,115','TCP / 換行 JSON：hello、heartbeat、command',532,101)
 + edge('320,158 740,158','station-status、command-ack（不傳影像）',532,147)
 + note(399,448,260,['本機監聽缺檔預設：','0.0.0.0 : 5000','允許連線預設關閉'])
 + note(20,575,1060,['部署必備：x64 原生相依、相機驅動 / Sapera、LSI 驅動與 DLL，以及可用的 Windows SQLite runtime。','TCP 目前無 TLS / 訊息認證；最新狀態發布不等同每筆檢測結果可靠送達。此圖為部署拓撲示意。']);
const deployment=svg('deployment',1100,661,'設備與多站部署圖','一個檢測工作站程序載入取像類別庫並連相機米輪，本地保存影像與資料庫。另一電腦以同套軟體的總監控角色，透過 TCP JSON 連接多個被控站。',p);

const source = (file,label=file) => `<a href="../${esc(file)}">${esc(label)}</a>`;
const form = name => source(`IntegratedImageProcessingApp/Forms/MainForm.${name}.cs`, `MainForm.${name}.cs`);
const svc = name => source(`IntegratedImageProcessingApp/Services/${name}.cs`,`${name}.cs`);
const diagrams=[
  {id:'architecture',en:'SYSTEM ARCHITECTURE',title:'系統架構圖',question:'先看責任邊界：程式分成哪些部分？',svg:architecture,
   points:['兩個專案：主程式負責檢測與操作；相機類別庫封裝 SDK、取像 UI 與米輪。','MainForm 的 partial 分檔共用欄位與生命週期；理解程式時須跨檔追蹤。','相機、手動確認與歷史重跑共用結果確認路徑，尚依賴部分 UI 控制項。'],
   refs:[source('IntegratedImageProcessingApp/Program.cs','Program.cs'),form('CameraCapture'),form('DetectionParameter.ResultReview'),source('CameraCaptureModule/Services/CameraService.cs','CameraService.cs')]},
  {id:'sequence',en:'SEQUENCE DIAGRAM',title:'相機檢測循序圖',question:'追一張影格：誰呼叫誰，哪些工作同時進行？',svg:sequence,
   points:['以本機相機影格為例；泳道代表邏輯責任，檢測工作仍由 MainForm 協調。','in-flight 旗標限制同時一張；忙碌時新影格略過，沒有待檢佇列。','背景保存持有獨立來源參照，於 finally 釋放；存圖失敗不改成從 TIFF 讀回檢測。'],
   refs:[source('IntegratedImageProcessingApp/Controls/CameraCaptureHostControl.cs','CameraCaptureHostControl.cs'),form('CameraCapture'),source('IntegratedImageProcessingApp/Controls/LargeImageSource.cs','LargeImageSource.cs')]},
  {id:'flow',en:'PROCESS FLOWCHART',title:'檢測處理流程圖',question:'掌握判定流程：輸入如何變成逐物件結果？',svg:flow,
   points:['一般缺陷包含平場核心與條件一至六；頻域一、二依啟用條件與整合需求執行。','「頻域」目前實作為加窗能量與可選鄰格比較；維護時依實際演算法判讀。','尺寸 A 優先，A 不符合才評估 B；未完成或資料不足的情況需保留待確認。逐物件判定彼此獨立。'],
   refs:[form('DetectionParameter.ResultReview'),form('DetectionParameter.DefectProcessing'),form('DetectionParameter.DefectFrequency'),form('DetectionParameter.ResultReview.GoodJudgement')]},
  {id:'data',en:'DATA FLOW DIAGRAM',title:'資料流與保存圖',question:'找設定與結果：檔案在哪裡，歷史如何重跑？',svg:data,
   points:['以下所有資料路徑以程式執行目錄為基準；工作參數、Recipe 和設備設定分開保存。','相機紀錄自動保存；手動確認是否記歷史、是否保留圖片，由兩個獨立選項控制。','歷史重跑精確使用批次 ParameterId；僅單參數舊快照可安全推定，舊多參數缺 ID 時拒絕猜測。'],
   refs:[svc('SystemParameterIniService'),svc('DetectionRecipeCatalogService'),svc('InspectionHistoryStore'),form('InspectionHistory')]},
  {id:'deployment',en:'DEPLOYMENT DIAGRAM',title:'設備與多站部署圖',question:'準備移機：要帶哪些依賴，設備怎麼連？',svg:deployment,
   points:['主程式與類別庫都指定 x64；方案顯示 Any CPU，部署仍需 x64 相依。','總監控與被控站使用同套軟體的不同角色；TCP 傳命令與狀態，沒有傳送原圖。','本機角色切換已有 INI 密碼驗證；這項機制未提供個人帳號、操作稽核或 TCP 身分驗證。'],
   refs:[source('IntegratedImageProcessingApp/IntegratedImageProcessingApp.csproj','主程式 csproj'),source('CameraCaptureModule/CameraCaptureModule.csproj','相機 csproj'),svc('InspectionTcpTransport'),form('ConnectionController'),form('Navigation')]}
];
function sectionDiagram(d,i){return `<section id="${d.id}" class="diagram-section tracked" aria-labelledby="heading-${d.id}">
  <header class="section-heading"><span class="section-number">0${i+1}</span><div><p class="eyebrow">${d.en}</p><h2 id="heading-${d.id}">${d.title}</h2><p class="section-question">${d.question}</p></div></header>
  <figure class="diagram-card"><div class="figure-toolbar"><span><i class="dot"></i>目前實作 · 圖 ${i+1} / 5</span><div class="figure-actions"><button data-expand="${d.id}" type="button" aria-label="放大${d.title}">放大檢視 ↗</button><button data-export="${d.id}" type="button" aria-label="下載${d.title} SVG">下載 SVG ↓</button></div></div><div class="diagram-viewport">${d.svg}</div><figcaption>圖 ${i+1}｜${d.question} 箭頭表示圖中標示的呼叫、處理或資料方向。</figcaption></figure>
  <div class="reading-notes"><h3>接手時要知道</h3><ul>${d.points.map(v=>`<li>${v}</li>`).join('')}</ul></div>
  <details class="source-details"><summary>對照原始碼</summary><div class="source-links">${d.refs.join('')}</div><p>原始碼連結以 HTML 位於專案 docs 目錄為準；只傳送這份 HTML 時，圖表與說明仍完整可讀。</p></details>
</section>`;}
const files=[
 ['SystemParameters.ini','目前工作參數與 [Password] 角色密碼','新建參數、套用 Recipe 仍保留密碼區段。'],
 ['recipe/parameterList.json<br>recipe/{ID}.ini','Recipe 目錄與參數快照','關聯以 ID 為主，名稱可修改。'],
 ['config/device-settings.ini','設備精度、站點、歷史開關、上次角色','更新程式時保留現場設定；X/Y 精度需實際標定。'],
 ['config/connection-controller-settings.xml','總控站點與連線行為','移機確認 IP、Port、ID、心跳與重連。'],
 ['captureSetting.ini','相機、觸發、預覽、存圖與米輪','另有相機模組自己的自動存圖選項。'],
 ['CameraCapture/yyyyMMdd<br>snapshot/yyyyMMdd','相機原圖 / 手動確認圖片副本','備份 DB 時一併保留被引用的圖片。'],
 ['history/yyyyMMdd.db<br>history/recipe-snapshots/{hash}.ini','每日檢測 DB / 去重歷史參數','保留圖片、快照與 ParameterId 才能重跑。']
];
const html=`<!doctype html>
<html lang="zh-Hant"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="color-scheme" content="light"><meta name="description" content="整合式影像處理軟體交接圖解：架構、循序、流程、資料流與部署。單檔離線文件。"><title>整合式影像處理軟體｜交接圖解</title>
<style>
:root{--nav:#10283f;--ink:#17334d;--muted:#587087;--blue:#185ec2;--line:#dce5ed;--paper:#f2f5f8;--surface:#fff;--green:#27785d;scroll-behavior:smooth;scroll-padding-top:28px}*{box-sizing:border-box}body{margin:0;background:var(--paper);color:var(--ink);font-family:"Segoe UI","Microsoft JhengHei",sans-serif;font-size:15px;line-height:1.8}a{color:var(--blue);text-underline-offset:3px}button{font:inherit;cursor:pointer}button:focus-visible,a:focus-visible,summary:focus-visible{outline:3px solid #e1a628;outline-offset:4px}.skip-link{position:fixed;left:20px;top:-70px;z-index:20;background:#fff;padding:8px 20px}.skip-link:focus{top:12px}.sidebar{position:fixed;inset:0 auto 0 0;width:246px;background:var(--nav);color:#e7eff7;padding:35px 23px;display:flex;flex-direction:column;z-index:5}.brand-mark{width:38px;height:38px;display:grid;place-items:center;border:1px solid #6990ad;border-radius:10px;font-size:19px;color:#cbe3fb;margin-bottom:18px}.brand{font-weight:700;font-size:17px;line-height:1.6;letter-spacing:.04em}.brand small{display:block;font-weight:400;font-size:12px;color:#adc2d4;letter-spacing:.13em;margin-top:7px}.nav-heading{font-size:11px;letter-spacing:.14em;color:#a2bace;margin:38px 12px 11px}.sidebar nav{display:grid;gap:7px}.sidebar nav a{display:flex;gap:12px;align-items:center;color:#c1d1df;text-decoration:none;padding:9px 12px;border-radius:7px;font-size:14px}.sidebar nav a:hover,.sidebar nav a.active{background:#254965;color:#fff}.nav-index{font-size:11px;color:#9db7d0;font-family:Consolas,monospace}.sidebar-bottom{margin-top:auto;border-top:1px solid #34516a;padding-top:19px;font-size:12px;color:#a9bfd1}.sidebar-bottom strong{font-weight:500;color:#e1edf7}.main{margin-left:246px;padding:34px 45px 55px;max-width:1600px}.topline{display:flex;justify-content:space-between;align-items:center;gap:15px;margin-bottom:23px;color:var(--muted);font-size:12px}.topline .breadcrumbs{letter-spacing:.06em}.pill{display:inline-flex;align-items:center;gap:7px;border:1px solid #d0dfd8;color:var(--green);border-radius:30px;padding:3px 12px;background:#f3faf7;font-size:12px}.dot{display:inline-block;width:7px;height:7px;background:#429879;border-radius:50%;margin-right:6px}.print-button{border:1px solid #bfd0de;background:#fff;color:var(--ink);padding:6px 15px;border-radius:7px}.hero{background:linear-gradient(115deg,#133653,#1c4e74);border-radius:16px;color:#fff;padding:42px 40px;position:relative;overflow:hidden}.hero:after{content:"";position:absolute;width:265px;height:265px;right:-95px;top:-135px;border:1px solid #6188a4;border-radius:50%;pointer-events:none}.eyebrow{font-size:11px;letter-spacing:.18em;margin:0 0 6px;color:var(--blue);font-weight:650}.hero .eyebrow{color:#afcde5}.hero h1{font-size:34px;line-height:1.4;letter-spacing:.025em;margin:10px 0 15px}.hero .lead{max-width:730px;font-size:16px;color:#d7e5ef;margin:0}.hero-meta{display:flex;flex-wrap:wrap;gap:8px;margin-top:25px}.hero-meta span{font-size:12px;border:1px solid #6a8aa3;border-radius:5px;padding:4px 10px;color:#e2edf6}.intro-grid{display:grid;grid-template-columns:1.2fr 1fr;gap:20px;margin:24px 0 42px}.intro-card{background:#fff;border:1px solid var(--line);border-radius:10px;padding:21px 24px}.intro-card h2{font-size:16px;margin:0 0 8px}.intro-card p{margin:0;color:var(--muted);font-size:14px}.route{display:flex;flex-wrap:wrap;gap:7px;margin-top:12px;font-size:13px}.route span{border-bottom:2px solid #c4d9ed;padding:2px 5px}.route b{color:#8ca0b1}.diagram-section{margin:0 0 45px;scroll-margin-top:25px}.section-heading{display:flex;align-items:center;gap:18px;margin-bottom:19px}.section-number{font-family:Consolas,monospace;font-size:33px;line-height:1;color:#678cac;border-right:1px solid #c8d7e3;padding:8px 17px 8px 0}.section-heading h2{font-size:25px;line-height:1.4;margin:0}.section-question{color:var(--muted);font-size:14px;margin:4px 0 0}.diagram-card{margin:0;background:#fff;border:1px solid #d3dfe9;border-radius:12px;overflow:hidden;box-shadow:0 5px 15px #17334d05}.figure-toolbar{display:flex;justify-content:space-between;align-items:center;gap:10px;border-bottom:1px solid #e2eaf0;padding:10px 20px;color:var(--muted);font-size:12px;background:#fbfcfe}.figure-actions{display:flex;gap:6px}.figure-actions button{border:0;background:transparent;color:#275b8b;font-size:12px;padding:5px 8px;border-radius:4px}.figure-actions button:hover{background:#e9f1f8}.diagram-viewport{overflow:auto;padding:19px 16px 8px}.diagram-viewport>svg{display:block;width:100%;height:auto;min-width:800px}figcaption{padding:10px 22px 15px;font-size:12px;color:var(--muted);border-top:1px solid #eff3f6}.reading-notes{display:grid;grid-template-columns:125px 1fr;gap:15px;margin-top:18px}.reading-notes h3{font-size:13px;color:#305d81;margin:2px 0}.reading-notes ul{margin:0;padding-left:19px;font-size:14px}.reading-notes li+li{margin-top:4px}.source-details{margin:15px 0 0 140px;border-top:1px solid var(--line);padding-top:10px;font-size:12px;color:var(--muted)}summary{cursor:pointer;color:#426683}.source-links{display:flex;flex-wrap:wrap;gap:7px 16px;margin-top:10px}.source-links a{font-family:Consolas,"Microsoft JhengHei",sans-serif;overflow-wrap:anywhere}.source-details p{margin:8px 0}.handoff-panel{background:#fff;border:1px solid var(--line);border-radius:12px;padding:27px 28px;margin-top:25px}.handoff-panel h2{margin:0 0 7px;font-size:23px}.handoff-panel h3{font-size:16px;margin:25px 0 8px}.handoff-panel p{margin:4px 0 13px;color:var(--muted);font-size:14px}.table-wrap{overflow-x:auto}table{border-collapse:collapse;width:100%;font-size:13px;text-align:left;margin:16px 0 4px}th{background:#eff4f8;color:#315674;font-weight:600}th,td{padding:12px 13px;border-bottom:1px solid #e1e8ee;vertical-align:top}td:first-child{font-family:Consolas,"Microsoft JhengHei",monospace;overflow-wrap:anywhere;min-width:200px}td:nth-child(2){min-width:200px}td:nth-child(3){min-width:230px}.steps{list-style:none;counter-reset:steps;padding:0;display:grid;grid-template-columns:1fr 1fr;gap:17px 25px;margin-top:20px}.steps li{position:relative;padding-left:40px;font-size:14px}.steps li:before{counter-increment:steps;content:counter(steps,decimal-leading-zero);position:absolute;left:0;top:2px;font-family:Consolas,monospace;color:#2369a4;font-size:17px}.steps strong{display:block}.steps span{color:var(--muted)}pre{background:#112c42;color:#e2edf7;padding:18px 20px;border-radius:8px;overflow:auto;font-size:12px;line-height:1.8}code{font-family:Consolas,"Microsoft JhengHei",monospace}.risk-tag{font-size:11px;white-space:nowrap;color:#865b1e;background:#fff0d1;border:1px solid #eddbad;border-radius:4px;padding:2px 7px;margin-right:6px}.risk-list{display:grid;gap:12px;margin:19px 0}.risk-item{border-left:3px solid #cda556;padding:2px 0 2px 14px;font-size:14px}.risk-item strong{font-weight:600}.risk-item p{font-size:13px;margin:4px 0}.risk-item .risk-source{font-size:12px;margin-top:3px}.notice{border:1px solid #d7e4ee;background:#f2f7fb;border-radius:8px;padding:14px 17px;font-size:13px;color:#395b77}.footer{border-top:1px solid #d5e0e9;margin-top:35px;padding-top:20px;display:flex;justify-content:space-between;gap:14px;font-size:12px;color:var(--muted)}.footer a{color:inherit}.print-only{display:none}dialog{border:1px solid #c2d3e0;border-radius:12px;width:95vw;max-width:1500px;height:94vh;max-height:1000px;padding:0;color:var(--ink);box-shadow:0 20px 90px #0004}dialog::backdrop{background:#0a2037b8}.modal-bar{display:flex;align-items:center;justify-content:space-between;padding:12px 22px;background:#f0f5fa;border-bottom:1px solid #d5e2ec}.modal-bar h2{font-size:17px;margin:0}.modal-bar button{background:#fff;border:1px solid #bdcddb;border-radius:5px;padding:4px 15px}.modal-body{height:calc(100% - 62px);overflow:auto;padding:20px;background:#fff}.modal-body svg{display:block;min-width:1100px;width:100%;height:auto}.toast{position:fixed;bottom:25px;right:25px;background:#153c58;color:#fff;padding:10px 18px;border-radius:7px;font-size:13px;z-index:30}.toast:empty{display:none}
@media(min-width:1550px){.main{padding-left:60px;padding-right:60px}.diagram-viewport{padding-left:32px;padding-right:32px}}
@media(max-width:1100px){.sidebar{width:205px;padding:28px 15px}.main{margin-left:205px;padding:26px}.hero{padding:30px}.hero h1{font-size:29px}.intro-grid{grid-template-columns:1fr}.intro-grid{gap:12px}.diagram-viewport>svg{min-width:920px}.reading-notes{grid-template-columns:1fr;gap:6px}.source-details{margin-left:0}}
@media(max-width:760px){.sidebar{position:relative;width:100%;padding:18px 20px}.brand-mark,.nav-heading,.sidebar-bottom,.brand small{display:none}.brand{font-size:16px}.sidebar nav{display:flex;overflow:auto;gap:5px;margin-top:11px;padding-bottom:3px}.sidebar nav a{white-space:nowrap;font-size:12px;padding:5px 9px}.sidebar nav a:first-child,.sidebar nav a:last-child{display:none}.nav-index{font-size:10px}.main{margin:0;padding:20px 15px 30px}.topline{font-size:11px;margin-bottom:16px}.breadcrumbs{max-width:170px}.hero{padding:26px 23px}.hero h1{font-size:26px}.hero .lead{font-size:14px}.hero-meta{gap:6px}.hero-meta span{font-size:11px}.intro-grid{margin-bottom:30px}.intro-card{padding:17px 20px}.section-number{font-size:27px}.section-heading{gap:12px}.section-heading h2{font-size:22px}.section-question{font-size:13px}.figure-toolbar{padding:9px 10px;flex-wrap:wrap}.figure-actions button{font-size:11px}.diagram-viewport{padding:12px 8px}.diagram-section{margin-bottom:33px}.handoff-panel{padding:22px 18px}.steps{grid-template-columns:1fr}.footer{flex-direction:column}.modal-body{padding:10px}figcaption{padding:10px 13px}}
@media(prefers-reduced-motion:reduce){:root{scroll-behavior:auto}}
@page{size:A4 landscape;margin:12mm}
@media print{:root{scroll-behavior:auto}body{background:#fff;font-size:11px;line-height:1.6}.sidebar,.topline,.figure-actions,dialog,.toast,.source-details{display:none!important}.main{margin:0;padding:0;max-width:none}.hero{background:#fff!important;border:1px solid #adc4d6;color:#17334d;padding:24px}.hero:after{display:none}.hero h1{font-size:29px}.hero .lead,.hero .eyebrow,.hero-meta span{color:#35546c}.hero-meta span{border-color:#c7d5df}.intro-grid{margin:18px 0;gap:16px}.intro-card{padding:17px}.diagram-section{break-before:page;break-inside:avoid;margin:0}.section-heading{margin-bottom:10px;gap:15px}.section-heading h2{font-size:21px}.section-question{font-size:12px}.section-number{font-size:26px}.diagram-card{border-radius:5px;box-shadow:none;overflow:visible}.figure-toolbar{padding:5px 13px;font-size:10px}.diagram-viewport{padding:4px 9px;overflow:visible;display:flex;justify-content:center}.diagram-viewport>svg{width:auto;height:auto;min-width:0;max-width:100%;max-height:124mm}figcaption{padding:4px 12px;font-size:10px}.reading-notes{grid-template-columns:100px 1fr;gap:10px;margin-top:9px}.reading-notes ul{font-size:11px}.reading-notes h3{font-size:11px}.reading-notes li+li{margin-top:1px}.handoff-panel{break-before:page;border:0;padding:0;margin:0}.handoff-panel h2{font-size:22px}.handoff-panel p{font-size:12px}.handoff-panel h3{font-size:14px;margin-top:16px}table{font-size:11px;margin-top:9px}th,td{padding:8px 10px;min-width:0!important}.steps{gap:15px;margin-bottom:15px}.steps li{font-size:12px}.risk-list{gap:9px}.risk-item{font-size:12px;break-inside:avoid}.risk-item p{font-size:11px}.risk-item .risk-source{font-size:10px}.notice{font-size:11px;padding:10px}.footer{font-size:10px;margin-top:16px}.print-only{display:block}a{color:inherit;text-decoration:none}*{-webkit-print-color-adjust:exact;print-color-adjust:exact}}
@media print{.skip-link{display:none!important}.footer{display:block;break-inside:avoid}.footer>span:last-child{display:none}}
</style></head><body>
<a class="skip-link" href="#overview">跳至交接內容</a>
<aside class="sidebar" aria-label="文件目錄"><div class="brand-mark" aria-hidden="true">▦</div><div class="brand">整合式影像處理軟體<small>TECHNICAL HANDOFF</small></div><div class="nav-heading">DOCUMENT MAP</div><nav>
<a href="#overview" class="active"><span class="nav-index">00</span>交接導讀</a>
${diagrams.map((d,i)=>`<a href="#${d.id}"><span class="nav-index">0${i+1}</span>${d.title}</a>`).join('')}
<a href="#files"><span class="nav-index">06</span>設定與資料位置</a><a href="#takeover"><span class="nav-index">07</span>接手與建置</a><a href="#risks"><span class="nav-index">08</span>待驗證與限制</a>
</nav><div class="sidebar-bottom"><strong>現況交接 · v1.0</strong><br>核對 ${verifiedDate}<br>程式基準 <code>${baseline}</code><br>五種圖解 / 單檔離線</div></aside>
<main class="main"><div class="topline"><span class="breadcrumbs">ENGINEERING / IMAGE INSPECTION</span><div><button class="print-button" id="print-document" type="button">列印 / 另存 PDF</button></div></div>
<section id="overview" class="tracked"><div class="hero"><p class="eyebrow">PROJECT KNOWLEDGE TRANSFER / 2026</p><h1>看懂系統，再接手維護。</h1><p class="lead">整合式影像處理軟體交接圖解。從程式責任、相機影格到檢測判定、資料保存與多站部署，用五種圖表建立完整的系統脈絡。</p><div class="hero-meta"><span>Windows WinForms</span><span>.NET Framework 4.7.2 / x64</span><span>2 個專案 · 5 種圖解</span><span>基準 ${baseline} · ${verifiedDate}</span></div></div>
<div class="intro-grid"><div class="intro-card"><h2>這份文件怎麼看</h2><p>交接對象：接手開發者、設備整合與調機工程師。建議先看架構與部署，再沿著一張影格追進程式。</p><div class="route"><span>架構</span><b>→</b><span>循序</span><b>→</b><span>流程</span><b>→</b><span>資料</span><b>→</b><span>部署</span></div></div><div class="intro-card"><h2><span class="pill"><i class="dot"></i>單檔離線文件</span></h2><p>圖表已內嵌，無需網路或額外圖檔。支援放大、下載 SVG 與列印。內容呈現目前實作；硬體、檢出效果與長時間負載仍需現場驗收。</p></div></div></section>
${diagrams.map(sectionDiagram).join('')}
<section id="files" class="handoff-panel tracked"><p class="eyebrow">CONFIGURATION & PERSISTENCE</p><h2>設定與資料位置</h2><p>下列路徑均相對於主程式執行目錄。移機或更新前，保存現場設定、Recipe、歷史 DB 與其引用的圖片／快照。</p><div class="table-wrap"><table><thead><tr><th scope="col">檔案 / 目錄</th><th scope="col">內容</th><th scope="col">交接重點</th></tr></thead><tbody>${files.map(row=>`<tr><td>${row[0]}</td><td>${row[1]}</td><td>${row[2]}</td></tr>`).join('')}</tbody></table></div><div class="notice" style="margin-top:20px">記憶體結果快照用來保留執行期間的結果與檢視狀態；程式關閉後消失。跨次啟動的追溯依賴磁碟上的歷史資料。</div></section>
<section id="takeover" class="handoff-panel tracked"><p class="eyebrow">TAKEOVER PLAYBOOK</p><h2>接手與建置</h2><p>先確認可還原的現場資料與依賴，再用固定影像／Recipe 建立接手基準。</p><ol class="steps"><li><strong>保留現場基準</strong><span>記錄 commit、設定、Recipe、X/Y 精度與代表影像；備份執行目錄的重要資料。</span></li><li><strong>補齊開發依賴</strong><span>Visual Studio / MSBuild、.NET Framework 4.7.2 Developer Pack、NuGet 套件與 SDK。</span></li><li><strong>覆寫硬體 SDK 路徑</strong><span>指定 CameraSdkDirectory 與 LsiNativeDirectory，確認 managed / native DLL 都是相容版本。</span></li><li><strong>以固定測例驗證結果</strong><span>先手動確認物件與 A/B/NG，再接相機與米輪；最後驗證總控、重連與連續運轉。</span></li></ol><h3>建置命令範例</h3><pre><code>msbuild .\\MyApp4.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" ^
  /p:CameraSdkDirectory="C:\\SDK\\Sapera" /p:LsiNativeDirectory="C:\\SDK\\LSI\\x64"</code></pre><p>上方換行使用 Windows cmd 的 <code>^</code>；在 PowerShell 請合併成一行。SDK 路徑為範例，需改成實際目錄。兩專案皆指定 x64。</p><div class="notice">舊式 csproj 逐一列出 Compile；新增 C# 檔案時同步加入專案檔。Repository 目前沒有獨立自動化測試專案。本次僅製作與核對交接文件，未重新建置或執行設備驗收。</div><h3>最少要帶走的驗收證據</h3><p>程式 commit、影像 hash／尺寸、Recipe hash、ParameterId、X/Y 精度、預期與實際判定、耗時／RAM、硬體與站點組態，以及失敗時的紀錄。完整案例見 <a href="VALIDATION_AND_LIMITATIONS.md">驗收與已知限制</a>。</p></section>
<section id="risks" class="handoff-panel tracked"><p class="eyebrow">KNOWN LIMITS & VERIFICATION</p><h2>接手優先確認的事項</h2><p>以下是目前路徑與資料模型的風險摘要，尚無本次實機通過證據。接手時應重現、修正並保留驗證結果。</p><div class="risk-list">
<div class="risk-item"><strong><span class="risk-tag">L01 · 優先</span>切換頁面可能中斷本機連續檢測轉送</strong><p>離開總覽會關閉 Overview 轉送，模式旗標可能保留且抑制自動檢測轉送。以影格序號驗證啟動、切頁、返回與停止。</p><div class="risk-source">來源：${form('InspectionOverview')} · ExitInspectionOverviewMode</div></div>
<div class="risk-item"><strong><span class="risk-tag">L02 · 優先</span>同站快速重連的舊連線清理</strong><p>舊 peer 的 finally 依 key 清理，可能移除同 key 新 peer。測試同 ID 重疊重連與多站操作。</p><div class="risk-source">來源：${svc('InspectionTcpTransport')} · ReadLoopAsync / RemovePeer</div></div>
<div class="risk-item"><strong><span class="risk-tag">L03</span>歷史重跑未固定當時設備精度</strong><p>歷史有參數快照，未包含當時全域 X/Y 精度；改標定後的 mm 量測與篩選結果可能改變。</p><div class="risk-source">來源：${svc('InspectionHistoryStore')}、${form('InspectionHistory')}</div></div>
<div class="risk-item"><strong><span class="risk-tag">L05 / L10</span>負載與保存完成狀態</strong><p>巢狀平行、背景存圖及 DB 缺少一致的資源／排空策略。測磁碟滿、關閉程式及多小時運轉，確認圖片和紀錄一致。</p><div class="risk-source">來源：${form('CameraCapture')}、${form('DetectionParameter.ResultReview')}</div></div>
<div class="risk-item"><strong><span class="risk-tag">L09</span>TCP 最新狀態不保證逐筆結果送達</strong><p>command-ack 是命令確認；目前沒有每筆檢測結果的可靠佇列與重送保證。以斷線／恢復案例確認現場需求。</p><div class="risk-source">來源：${svc('InspectionTcpTransport')}、${form('ConnectionController')}</div></div>
</div><div class="notice"><strong>文件版本差異：</strong>既有 docs 架構與驗收文件以 2026-10-30 / b512791 為基準，部分角色描述已較舊。本 HTML 已核對目前 ${baseline} 的角色密碼流程：選角色後按「套用」，依角色規則驗證，成功後才更新角色；密碼位於 SystemParameters.ini 的 [Password]。</div><h3>繼續閱讀</h3><p><a href="../PROJECT_HANDOFF.md">完整交接說明</a> · <a href="CURRENT_ARCHITECTURE.md">目前架構</a> · <a href="SYSTEM_SPECIFICATION.md">功能規格</a> · <a href="USER_MANUAL.md">操作手冊</a> · <a href="VALIDATION_AND_LIMITATIONS.md">驗收與已知限制</a> · <a href="SOURCE_TRACE.md">程式對照表</a></p></section>
<footer class="footer"><span>整合式影像處理軟體 · 交接圖解 v1.0<br>核對日期 ${verifiedDate} · 程式基準 ${baseline} · 內容為靜態程式核對</span><span>單檔可轉交 · 圖表可匯出 SVG<br><a href="#overview">回到導讀 ↑</a></span></footer></main>
<dialog id="diagram-modal" aria-labelledby="modal-title"><div class="modal-bar"><h2 id="modal-title">圖表放大</h2><button type="button" id="close-modal">關閉 ×</button></div><div class="modal-body" id="modal-body"></div></dialog><div class="toast" id="toast" role="status" aria-live="polite"></div>
<script>
(() => {
  'use strict';
  const modal=document.getElementById('diagram-modal');
  const modalBody=document.getElementById('modal-body');
  const modalTitle=document.getElementById('modal-title');
  const toast=document.getElementById('toast');
  let timer;
  const notify=message=>{toast.textContent=message;clearTimeout(timer);timer=setTimeout(()=>{toast.textContent='';},2600);};
  document.getElementById('print-document').addEventListener('click',()=>window.print());
  document.getElementById('close-modal').addEventListener('click',()=>modal.close());
  modal.addEventListener('click',event=>{if(event.target===modal){const r=modal.getBoundingClientRect();if(event.clientX<r.left||event.clientX>r.right||event.clientY<r.top||event.clientY>r.bottom)modal.close();}});
  document.querySelectorAll('[data-expand]').forEach(button=>button.addEventListener('click',()=>{
    const id=button.dataset.expand;
    const original=document.getElementById('diagram-'+id);
    const clone=original.cloneNode(true);
    // Rewrite cloned IDs and references so accessibility and marker URLs stay unique.
    const nodes=[clone,...clone.querySelectorAll('[id]')];
    const map=new Map(nodes.filter(n=>n.id).map(n=>[n.id,n.id+'-expanded']));
    nodes.forEach(n=>{if(n.id)n.id=map.get(n.id);});
    [clone,...clone.querySelectorAll('*')].forEach(n=>[...n.attributes].forEach(attr=>{
      let value=attr.value;
      map.forEach((next,prev)=>{value=value.replaceAll('url(#'+prev+')','url(#'+next+')');});
      if(attr.name==='aria-labelledby')value=value.split(' ').map(v=>map.get(v)||v).join(' ');
      if(value!==attr.value)n.setAttribute(attr.name,value);
    }));
    modalTitle.textContent=document.getElementById('heading-'+id).textContent;
    modalBody.replaceChildren(clone);modal.showModal();modalBody.scrollTo(0,0);
  }));
  document.querySelectorAll('[data-export]').forEach(button=>button.addEventListener('click',()=>{
    const id=button.dataset.export;
    const serialized=new XMLSerializer().serializeToString(document.getElementById('diagram-'+id));
    const url=URL.createObjectURL(new Blob(['<?xml version="1.0" encoding="UTF-8"?>\\n'+serialized],{type:'image/svg+xml;charset=utf-8'}));
    const a=document.createElement('a');a.href=url;a.download='handoff-'+id+'.svg';document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),1500);notify('已下載 SVG 圖表');
  }));
  if('IntersectionObserver' in window){
    const links=[...document.querySelectorAll('.sidebar nav a')];
    const observer=new IntersectionObserver(entries=>{
      const visible=entries.filter(e=>e.isIntersecting).sort((a,b)=>a.boundingClientRect.top-b.boundingClientRect.top);
      if(!visible.length)return;
      links.forEach(a=>{const active=a.hash==='#'+visible[0].target.id;a.classList.toggle('active',active);if(active)a.setAttribute('aria-current','location');else a.removeAttribute('aria-current');});
    },{rootMargin:'-5% 0px -65% 0px',threshold:0});
    document.querySelectorAll('.tracked').forEach(s=>observer.observe(s));
  }
})();
</script></body></html>`;
fs.writeFileSync(out,html,'utf8');
console.log(`Generated ${path.relative(root,out)} (${Buffer.byteLength(html).toLocaleString()} bytes), baseline ${baseline}`);
