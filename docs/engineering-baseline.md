# 极简录屏工具 项目方案规划书

**版本：v1.2-r2 ｜ 日期：2026-10-05 ｜ 状态：冻结前工程基线（发布与媒体行为修订）**

> v1.2-r2 冻结的是架构边界、生命周期原则、产品行为和阶段 0 Gate。  
> **具体接口名称与方法签名暂不冻结**，必须在阶段 0 Spike 验证 ScreenRecorderLib 的真实能力后再锁定。  
> 阶段 0 通过后，再生成“架构冻结版”，届时锁定接口签名与底层技术路线。

---

# 1. 项目概述

面向 Windows 的极简录屏软件。核心体验只有三步：

> **选区 → 声音开关 → 开始**

分辨率、帧率、画质、输出路径、设备选择、热键等进阶选项全部收进「设置」，不干扰主流程。

产品目标不是替代 OBS，而是提供一个启动快、操作少、长时间录制可靠、异常情况下尽可能保住已录内容的轻量工具。

---

# 2. 背景、目标与非目标

### 2.1 用户痛点

OBS、ShareX、Xbox Game Bar 等工具功能强大，但对于只想快速录制一块屏幕区域的用户，存在功能过多、路径过长或选区体验不够直接的问题。

### 2.2 核心目标

- 3 步内开始录制，首次使用无需教程。
- 主界面保持极简，高级能力全部收进设置页。
- 支持手动区域、窗口、显示器三种捕获目标。
- 支持系统声音与麦克风独立开关。
- 长时间录制稳定。
- 磁盘不足、音频设备断开、录制引擎报错时统一安全收尾。
- 程序异常退出后，能够识别残留录制，不静默删除非空临时媒体。
- 状态悬浮窗和录制边框对用户可见，但不进入成品画面。
- UI、业务流程和具体录制引擎相互解耦，以便未来更换底层实现。
- 多屏、混合 DPI、锁屏、睡眠、显示器变化等 Windows 生命周期事件有明确处理策略。

### 2.3 v1 非目标

v1 不做：

- 视频剪辑
- 直播推流
- 多场景合成
- 摄像头画中画
- 专业音频混音
- 云同步
- OCR / AI 总结
- 跨显示器自由区域录制
- 自动修复任意损坏 MP4
- 暂停 / 继续录制

---

# 3. 支持系统

### 3.1 技术最低版本

> **Windows 10 Version 2004（Build 19041）及以上 / Windows 11**

原因：v1 把 `WDA_EXCLUDEFROMCAPTURE` 作为核心 Overlay 排除能力，不再维护旧版 Windows 的降级分支。

### 3.2 发布测试主线

由于 Windows 10 2004 已不是当前受支持的主流版本，实际发布测试优先覆盖：

- Windows 10 22H2
- Windows 11 当前受支持版本

技术最低版本和主测试版本是两个概念。

---

# 4. 功能范围与优先级

| 功能 | 说明 | 优先级 |
| --- | --- | --- |
| 手动选区 | v1 限定在单个显示器内拖拽 | P0 |
| 窗口选区 | 悬停高亮、点击选定 | P0 |
| 整屏选区 | 按显示器选择 | P0 |
| 声音开关 | 系统声音、麦克风独立控制 | P0 |
| 音频设备绑定 | 录制与音量条使用相同设备 ID | P0 |
| 开始 / 停止 | 按钮、托盘、全局热键；注册冲突有提示 | P0 |
| 状态悬浮窗 | 文件大小、双路音量条 | P0 |
| 排除捕获 | 悬浮窗和边框不进入成品视频 | P0 |
| Session 工作目录 | 同卷临时媒体、原子 metadata、命名与文件系统预检 | P0 |
| 最小异常恢复 | 识别残留 Session，不删除非空媒体 | P0 |
| Stop 超时保护 | 防止永久卡在 Stopping | P0 |
| 多屏 / 混合 DPI | PerMonitorV2 + 负坐标验证 | P0 |
| 高级设置 | 分辨率、帧率、画质、输出目录、设备 | P1 |
| 设备热插拔 | 耳机 / 麦克风变化不导致主程序崩溃 | P1 |
| 系统生命周期 | 锁屏、睡眠、显示器变化、关机注销 | P1 |
| 日志 | 每次 Session 参数、状态、异常可追踪 | P1 |
| 选区记忆 | 记住上一次区域 | P2 |
| 倒计时 | 开始前 3 / 5 秒倒计时 | P2 |
| 暂停 / 继续 | 后续扩展 | P2 |
| 鼠标点击高亮 | 后续扩展 | P2 |

---

# 5. 架构硬约束

本节是 **阶段 0 前真正冻结的内容**。

## 5.1 依赖方向

正确依赖方向为：

```text
                    ScreenRecorder.App
                    /       |       \
                   v        v        v
              UI.Wpf     Core     Infrastructure.Windows
                 |         ^              |
                 |         |              |
                 +-------->+<-------------+
```

也可以简写为：

```text
UI.Wpf → Core ← Infrastructure.Windows

App → Core
App → UI.Wpf
App → Infrastructure.Windows
```

**禁止：**

```text
Core → Infrastructure.Windows
UI.Wpf → Infrastructure.Windows
```

这样不会出现：

```text
Core 需要 IRecordingBackend
↓
IRecordingBackend 却定义在 Infrastructure
↓
Infrastructure 又需要 Core 的 CaptureTarget
↓
循环引用
```

## 5.2 Ports / Abstractions 全部定义在 Core

以下是平台能力的**接口**，接口放在 `ScreenRecorder.Core`：

```text
IRecordingBackend
IRecordingBackendFactory
IAudioMeterService
IAudioDeviceService
ISettingsStore
IFileSystem
IWindowQueryService
IDisplayService
ICaptureExclusionService
IPowerRequestService
ISystemEventSource
```

具体实现放在：

```text
ScreenRecorder.Infrastructure.Windows
```

例如：

```text
IWindowQueryService
        ↑
Win32WindowQueryService
```

```text
ICaptureExclusionService
        ↑
Win32CaptureExclusionService
```

```text
IRecordingBackendFactory
        ↑
ScreenRecorderLibBackendFactory
```

## 5.3 UI 可以使用平台能力，但只能通过 Core Port

原“UI 不得调用 Win32”改为更准确的：

> **UI 不得直接 P/Invoke，不得引用 Infrastructure；UI 可以调用 Core 中定义的平台接口。**

例如窗口选择器：

```text
RegionSelector
   ↓
IWindowQueryService
   ↓
Infrastructure.Windows / Win32
```

排除自己窗口：

```text
StatusWindow
   ↓ 传 HWND
ICaptureExclusionService
   ↓
SetWindowDisplayAffinity
```

UI 只传：

- HWND
- MonitorId
- PixelRect
- 用户操作意图

不接触 P/Invoke 声明。

## 5.4 第三方库按“所属层”隔离，而不是全部塞进 Infrastructure

冻结规则：

- ScreenRecorderLib → `Infrastructure.Windows`
- NAudio → `Infrastructure.Windows`
- Win32 P/Invoke → `Infrastructure.Windows`
- 文件日志 Sink → 通常 `Infrastructure.Windows` / App 注册
- WPF 专用托盘库（若使用）→ `UI.Wpf`
- DI / Host 相关库 → `App`
- Core → 不引用这些第三方实现包

**核心要求不是“所有第三方都在 Infrastructure”，而是第三方类型不得穿透层边界。**

## 5.5 其他硬规则

1. UI 不得引用 ScreenRecorderLib。
2. UI 不得引用 NAudio。
3. UI 不得直接 P/Invoke。
4. Core 不依赖 WPF 的 `Rect`、`Window`、`Dispatcher` 等 UI 类型。
5. Core 内所有屏幕坐标统一为 Physical Pixel。
6. UI 窗口关闭不得直接决定 Recorder 生命周期。
7. 所有 Stop 原因必须汇聚到同一个 Core 生命周期流程。
8. Start / Stop 必须防重入。
9. Session 媒体不得因为应用异常而被静默删除。
10. App 只做 Composition Root，不承载业务规则。

---

# 6. 技术选型

| 用途 | 选型 |
| --- | --- |
| 语言与运行时 | C# / .NET 8 |
| UI | WPF |
| 首选录制后端 | ScreenRecorderLib |
| 音量电平 | NAudio / WASAPI |
| 系统能力 | Win32 P/Invoke |
| 日志抽象 | `Microsoft.Extensions.Logging` |
| 文件日志实现 | 优先 `Serilog.Extensions.Logging` + `Serilog.Sinks.File` |
| 配置 | JSON |
| 备选录制后端 | Windows.Graphics.Capture + FFmpeg |

> `Microsoft.Extensions.Logging` 本身是日志抽象，不自带通用滚动文件输出，因此必须配具体 Provider / Sink。

## 6.1 ScreenRecorderLib 运行时依赖

ScreenRecorderLib 当前是 C++/CLI / native Media Foundation 组合实现，运行时依赖不能只按“.NET 8 自包含”理解。

发布包必须考虑：

```text
.NET Runtime
→ 可由 self-contained publish 携带

VC++ Redistributable
→ ScreenRecorderLib 额外依赖，self-contained .NET 不会替你解决

Media Foundation
→ 普通桌面版 Windows 通常具备
→ Windows N / KN 或特殊 Server 环境可能缺失
```

因此发布验收不再写成：

```text
“未安装 .NET Runtime 的机器能运行”
```

而改成：

> **在干净的受支持 Windows 10 / Windows 11 虚拟机上，从最终安装包 / Portable 包完成安装或启动并成功录制。**

安装包必须：

- 检测所需 VC++ Redistributable；
- 缺失时引导或捆绑官方 redistributable 安装；
- 对缺失 Media Foundation 的系统给出明确依赖错误；
- 不把 `DllNotFoundException / module could not be found` 暴露给普通用户。

### Single-file 发布

不把“单文件 EXE”列为 v1 必达目标。

原因：

- ScreenRecorderLib 含 managed C++ / native 组件；
- .NET 官方也提示 managed C++ 组件并不适合 single-file deployment；
- native 依赖通常仍涉及提取 / 并排部署。

阶段 6 必须实际测试：

```text
Folder self-contained
Single-file self-contained
Installer
Portable ZIP
```

v1 默认发布候选：

> **self-contained folder / installer**

只有实测稳定后才提供 single-file 版本。


## 6.2 录制技术路线

### 路线 A：ScreenRecorderLib

ScreenRecorderLib 负责实际：

- Capture
- 编码
- 音频
- MP4 输出

这是首选。

### 路线 B：ScreenRecorderLib + 平台适配 / workaround

ScreenRecorderLib 仍然负责实际捕获和编码。

自有代码只补：

- 窗口 / 显示器枚举
- DPI / 坐标归一
- Window geometry tracking
- Overlay 排除
- 生命周期事件
- 输出尺寸策略
- Session / Recovery
- 设备预检

**路线 B 不再写“WGC 辅助捕获”。**

如果已经需要 WGC 真正接管帧捕获，就不再属于“小 workaround”，应直接评估路线 C，避免两条捕获管线混在一起。

### 路线 C：WGC + FFmpeg

仅在阶段 0 证明 ScreenRecorderLib 核心能力无法满足要求时启用。

---

# 7. 分层职责

## 7.1 ScreenRecorder.App

只负责：

- 应用入口
- DI / Composition Root
- 单实例
- 全局异常边界
- 组装 UI、Core、Infrastructure
- 启动 Recovery 扫描
- 应用关闭协调

不放业务规则。

## 7.2 ScreenRecorder.UI.Wpf

包含：

- RegionSelector
- ControlBar
- StatusWindow
- RecordingBorderWindow
- Settings
- Tray UI

UI 负责：

- 绘制
- 收集用户输入
- 调用 Core Service / Port
- 订阅 Core AppState

UI 不负责：

- Session 创建
- 输出路径生成
- Backend 创建
- Encoder Dispose
- Recovery 状态修改

## 7.3 ScreenRecorder.Core

包含：

- AppStateMachine
- RecorderService
- RecordingSession
- SessionManager
- RecoveryService
- StorageGuard
- CaptureTarget
- RecordingOptions
- 所有 Port 接口
- Start / Stop / Recovery 策略

## 7.4 ScreenRecorder.Infrastructure.Windows

实现 Core Ports：

- ScreenRecorderLib backend
- NAudio meter / device service
- Win32 window enumeration
- monitor / DPI service
- capture exclusion
- power request
- Windows system events
- Windows file system
- logging file sink
- future WGC + FFmpeg backend

---

# 8. 核心模型

## 8.1 PixelRect / PixelSize

```csharp
public readonly record struct PixelRect(
    int X,
    int Y,
    int Width,
    int Height);

public readonly record struct PixelSize(
    int Width,
    int Height);
```

Core 内：

- 单位始终为 Physical Pixel
- 不使用 WPF DIP
- H.264 需要时统一校正到偶数尺寸

## 8.2 CaptureTarget

```csharp
public abstract record CaptureTarget;

public sealed record AreaCaptureTarget(
    string MonitorId,
    PixelRect Bounds
) : CaptureTarget;

public sealed record WindowCaptureTarget(
    nint Hwnd
) : CaptureTarget;

public sealed record MonitorCaptureTarget(
    string MonitorId
) : CaptureTarget;
```

### Area v1 约束

- 只允许落在一个显示器内
- `AreaCaptureTarget` 必须携带 `MonitorId`
- `Bounds` 必须 clip 到对应显示器 Physical Pixel 范围
- 不允许跨屏框选

这显著降低：

- 混合 DPI
- 负坐标
- 不同刷新率 / 分辨率
- 跨屏裁剪

带来的复杂度。

## 8.3 RecordingOptions

`RecordingOptions` 只描述**已经解析完成的一次录制参数**，不拥有文件路径。

示意：

```csharp
public sealed record RecordingOptions(
    PixelSize OutputSize,
    int FrameRate,
    VideoQualityPreset Quality,
    bool RecordSystemAudio,
    bool RecordMicrophone,
    string? SystemAudioDeviceId,
    string? MicrophoneDeviceId);
```

规则：

- `OutputSize` 在开始前解析为固定值。
- 视频编码过程中分辨率不变化。
- `FrameRate` 表示**用户目标帧率**，不自动等价于“输出一定是 CFR”。
- 是否启用 fixed-framerate 必须由阶段 0 结果决定，并作为 Backend 配置显式保存。
- “使用默认设备”只是 Settings 层的用户意图。
- Session 真正开始前必须把“默认设备”解析为具体设备 ID。
- Backend 和 AudioMeter 使用**同一个已解析设备 ID**。

这样不会出现：

```text
音量条跟着新默认设备跳
但 Recorder 仍在录旧设备
```

或反过来。

## 8.4 文件路径不属于 RecordingOptions

由 Core 的 SessionManager / OutputPlanner 负责：

```text
OutputDirectory
FilenamePolicy
    ↓
WorkingDirectory
TempMediaPath
FinalMediaPath
```

Backend 只接收 Core 已经决定好的临时媒体路径。

## 8.5 RecordingSession

Session 是一次录制的持久化记录。

示意：

```csharp
public sealed class RecordingSession
{
    public Guid Id { get; init; }

    public CaptureTarget Target { get; init; } = default!;
    public RecordingOptions Options { get; init; } = default!;

    public string WorkingDirectory { get; init; } = "";
    public string TempMediaPath { get; init; } = "";
    public string FinalMediaPath { get; init; } = "";

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; private set; }

    public SessionStatus Status { get; private set; }
    public StopReason? StopReason { get; private set; }
}
```

**不得公开 `set`。**

Session 状态只能由 Core 的 SessionManager / RecordingService 生命周期方法修改。

---

# 9. 状态的唯一真源

这是 v1.2 的关键修正。

## 9.1 AppState 是 UI 流程唯一真源

```text
IAppStateMachine.CurrentState
```

是以下流程状态的唯一权威：

```text
Idle
Selecting
Ready
Starting
Recording
Stopping
Faulted
```

UI 只订阅：

```text
IAppStateMachine.StateChanged
```

## 9.2 RecorderService 不维护第二套公开状态

RecorderService：

- 负责串行执行生命周期命令
- 调用 AppStateMachine 做合法状态转移
- 不再额外维护一套 `RecorderState`
- 不再向 UI 暴露重复的 `RecorderStateChangedEventArgs`

如果需要 `State` 属性：

```text
RecorderService.State
```

也必须只是：

```text
_stateMachine.CurrentState
```

的只读投影。

## 9.3 SessionStatus 不是第二个 AppState

`SessionStatus` 是**持久化媒体生命周期**，例如：

```text
Created
Starting
Recording
Finalizing
Completed
FailedToStart
Interrupted
StopFailed
Orphaned
```

它不驱动 UI 页面流程。

关系：

```text
AppState
= 当前应用正在做什么

SessionStatus
= 这份媒体文件最终处于什么状态
```

两者语义不同，不能互相替代。

---

# 10. Backend 生命周期

## 10.1 Backend 接口定义在 Core

示意接口：

```csharp
public interface IRecordingBackend : IAsyncDisposable
{
    event EventHandler<RecordingBackendFailedEventArgs>? Failed;

    Task StartAsync(
        RecordingBackendStartRequest request,
        CancellationToken cancellationToken);

    Task StopAsync(
        CancellationToken cancellationToken);
}
```

其中：

```csharp
public sealed record RecordingBackendStartRequest(
    CaptureTarget Target,
    RecordingOptions Options,
    string TempMediaPath);
```

> 以上是阶段 0 前示意签名，不冻结具体类型名和参数形式。

## 10.2 每个 Session 创建全新 Backend

增加：

```csharp
public interface IRecordingBackendFactory
{
    IRecordingBackend Create();
}
```

规则：

```text
Session A
    ↓
Factory.Create()
    ↓
Backend A
    ↓ Stop / Dispose

Session B
    ↓
Factory.Create()
    ↓
Backend B
```

不得默认复用上一个 Session 的 Recorder 对象。

目的：

- 避免旧事件订阅残留
- 避免底层对象不可重入
- 避免状态污染
- 便于失败后放弃实例

## 10.3 Failed 回传

Backend 必须能把异步运行时失败上报 Core：

```text
ScreenRecorderLib OnRecordingFailed
            ↓
IRecordingBackend.Failed
            ↓
RecorderService
            ↓
统一 Stop 流程
```

Backend：

- 不直接切 AppState
- 不弹 UI
- 不在回调线程同步等待完整 Stop

---

# 11. 状态机与完整失败路径

## 11.1 正常主线

```text
Idle
 ↓
Selecting
 ↓
Ready
 ↓
Starting
 ↓
Recording
 ↓
Stopping
 ↓
Idle
```

## 11.2 Faulted

`Faulted` 只用于一种情况：

> **无法确认录制后端已经停止 / 释放，因此继续开始新录制不安全。**

例如：

```text
Backend Stop 超时
+
Dispose 也无法在二次超时内完成
```

普通录制错误仍然走：

```text
Recording → Stopping → Idle
```

不会轻易进入 Faulted。

## 11.3 状态转移表

| 当前状态 | 事件 | 下一状态 | 处理 |
| --- | --- | --- | --- |
| Idle | BeginSelection | Selecting | 不创建 Session |
| Selecting | TargetConfirmed | Ready | 保存 Target |
| Selecting | Esc | Idle | 清选区 |
| Ready | StartRequested | Starting | 开始 Preflight；**此时还不建 Session** |
| Ready | Esc | Idle | 清 Target |
| Starting | PreflightFailed，Target 有效 | Ready | 不建 Session / 清空空资源 |
| Starting | TargetInvalidated | Idle | 清 Target |
| Starting | SessionCreated + BackendStarted | Recording | Session → Recording |
| Starting | StopRequested | Starting | 记录 PendingStopReason；不得并发调用 Backend.Stop |
| Starting | BackendStartFailed，Target 有效 | Ready | Session → FailedToStart 或 InterruptedDuringStart |
| Starting | BackendStartFailed，Target 失效 | Idle | Session 同上，清 Target |
| Recording | User / Tray / Hotkey Stop | Stopping | StopReason=UserRequested |
| Recording | DiskLow | Stopping | StopReason=DiskSpaceLow |
| Recording | Backend.Failed | Stopping | StopReason=BackendFailure |
| Recording | DevicePolicyStop | Stopping | StopReason=DeviceDisconnected |
| Recording | SessionLock / Suspend / DisplayChange | Stopping | 使用对应 StopReason |
| Recording | Shutdown / Logoff | Stopping | best-effort bounded stop |
| Stopping | Stop+Flush+Finalize 成功 | Idle | Session → Completed / Interrupted |
| Stopping | Backend Stop 超时但 Dispose 成功 | Idle | Session → StopFailed，保留媒体 |
| Stopping | Backend 无法确认静止 | Faulted | 禁止新录制，提示重启 |
| Faulted | AppRestart | Idle | RecoveryService 先扫描 |

## 11.4 Starting 中收到 Stop

不能简单忽略，也不能在 `StartAsync` 还没返回时并发调用底层 `Stop()`。

规则：

```text
Starting
   ↓ StopRequested
记录 PendingStopReason
   ↓
尽力取消 Start（如果 Backend 支持）
   ↓
等待 Start 得出明确结果
   ├─ Start 成功 → 立即进入 Stopping
   └─ Start 失败 → 按 StartFailed 收尾
```

这样避免底层录制库的 Start / Stop 重入。

---

# 12. Start 生命周期

UI 只发：

```text
StartRequested(Target, UserAudioChoices)
```

以下流程全部在 Core 内完成。

## 12.1 正确顺序

```text
Ready
 ↓
Starting
 ↓
1. 验证 Target 仍有效
 ↓
2. 读取 Settings
 ↓
3. 解析固定 OutputSize
 ↓
4. 解析具体系统输出 / 麦克风设备 ID
 ↓
5. 麦克风访问 / 设备可用性预检
 ↓
6. 输出目录写权限测试
 ↓
7. 磁盘空间预检
 ↓
8. 所有 Preflight 通过
 ↓
9. SessionManager 规划同卷路径
 ↓
10. 创建 Session 工作目录
 ↓
11. 原子写 session.json
 ↓
12. BackendFactory.Create()
 ↓
13. 订阅 Backend.Failed
 ↓
14. Backend.StartAsync(...)
 ↓
Recording
```

关键点：

> **先 Preflight，再创建 Session。**

否则只是因为：

- 输出目录没权限
- 目标窗口已经没了
- 麦克风不可用
- 磁盘根本不够

就留下空 Session，RecoveryService 下次会把它误判为异常残留。

## 12.2 麦克风隐私 / 访问失败

预检至少要能区分：

```text
设备不存在 / 已断开
无法打开设备
访问被系统设置或策略阻止
```

无法可靠区分具体原因时，也必须给出：

```text
“无法访问所选麦克风”
```

并提供：

```text
继续录制但关闭麦克风
或
取消开始
```

不能无提示地开始一段用户以为“有麦”的录像。

---

# 13. Stop 生命周期与超时

## 13.1 所有 Stop 汇聚

来源可能包括：

- UI
- 热键
- 托盘
- StorageGuard
- Backend.Failed
- 设备断开
- 锁屏
- 睡眠
- 显示器变化
- 程序关闭
- 系统注销 / 关机

全部进入：

```text
RecorderService.StopAsync(reason)
```

## 13.2 幂等

至少满足：

```text
Idle      + Stop → no-op
Starting  + Stop → PendingStop
Recording + Stop → 执行一次
Stopping  + Stop → 等待已有 Stop / no-op
Faulted   + Stop → no-op
```

Core 内部使用串行化机制，例如：

```csharp
SemaphoreSlim
```

具体实现不冻结。

## 13.3 Stop 必须有超时

不能允许：

```text
Backend flush
↓
永久卡住
↓
AppState 永远 Stopping
```

因此 Stop 必须有：

```text
Soft Stop Timeout```

建议初始值：

> **30 秒，阶段 0 / 阶段 4 实测后调整。**

超时后：

1. 原子更新 `session.json`
2. `SessionStatus = StopFailed`
3. 保留非空临时媒体
4. 发出 cancellation
5. best-effort `DisposeAsync`
6. Dispose 也必须有二次超时

### 重要：不承诺“强杀线程”

进程内第三方录制库一旦卡死：

- 不能安全 `Thread.Abort`
- 不能保证 Dispose 一定返回
- 不能为了回 Idle 强行删除文件

所以：

```text
Dispose 成功
→ Idle
→ Recovery 后续处理临时媒体
```

```text
Dispose 仍无法确认后端静止
→ Faulted
→ 禁止开始下一次录制
→ 提示保存当前状态并重启应用
```

---

# 14. Session、文件与 Recovery

## 14.1 Session 工作目录必须与最终输出同卷

禁止：

```text
临时媒体：%AppData% / C:
最终视频：D:
```

否则几十 GB 文件在 Finalize 时会变成跨卷复制。

建议：

```text
D:\Recordings\
├─ final-video.mp4
└─ .screenrecorder\
   └─ sessions\
      └─ <session-id>\
         ├─ recording.partial.mp4
         ├─ session.json
         └─ session.log
```

最终完成：

```text
recording.partial.mp4
        ↓ 同一卷内 move / rename
final-video.mp4
```

目标：

- 不需要双倍磁盘空间
- Finalize 尽量只做文件系统重命名 / 移动
- 降低几十 GB 文件 Finalize 的风险

## 14.2 session.json 原子更新

禁止直接覆盖原文件。

采用：

```text
session.json.tmp
      ↓ write + flush
原子 replace / rename
      ↓
session.json
```

如果程序正好崩在写 metadata：

- 要么保留旧完整版本
- 要么得到新完整版本
- 避免半截 JSON

具体 Replace API 由 Infrastructure 文件系统实现决定。

## 14.3 Recovery Index

因为 Session 工作目录分散在用户选择的输出盘，建议 `%AppData%` 仅保存一个很小的恢复索引：

```text
recovery-index.json
```

内容只保存：

```text
SessionId
WorkingDirectory
```

不保存视频。

索引同样原子写。

启动时：

1. 读取 Recovery Index
2. 扫描当前输出目录的 `.screenrecorder/sessions`
3. 合并结果
4. 对不可访问 / 已拔掉的外置盘给出可理解状态

## 14.4 P0 Recovery

P0 只承诺：

- 识别非终态 Session
- 识别非空 partial 媒体
- 不自动删除
- 标记 `Interrupted / StopFailed / Orphaned`
- 提供“打开文件夹”
- 记录诊断
- 为未来 remux / repair 留接口

P0 不承诺自动修复任意损坏视频。

## 14.5 文件命名与重名

默认文件名：

```text
yyyy-MM-dd_HH-mm-ss.mp4
```

例如：

```text
2026-10-05_14-32-18.mp4
```

如果同名：

```text
2026-10-05_14-32-18_001.mp4
2026-10-05_14-32-18_002.mp4
```

由 Core 的 OutputPlanner 在创建 Session 前原子预留最终路径，避免并发 / 快速连续录制发生覆盖。

禁止静默覆盖已有录像。

## 14.6 文件系统限制

Start Preflight 必须读取目标卷：

```text
File system
Available free space
Writable
```

v1 对 FAT32：

> **不允许作为录像输出卷。**

原因是 FAT32 单文件大小上限约 4 GiB，不适合长时间录像。

提示用户选择：

```text
NTFS
或
exFAT
```

不采用“录到接近 4GB 再自动停”的复杂分支。

---


# 15. 帧率与 MP4 容器策略

## 15.1 ScreenRecorderLib 的默认帧率语义

当前 ScreenRecorderLib 的 `VideoEncoderOptions` 默认：

```text
Framerate = 30
IsFixedFramerate = false
```

`IsFixedFramerate=true` 的语义是：

> 即使桌面内容没有变化，也按目标时间间隔向编码器发送帧，必要时重复前一帧。

因此 UI 里的：

```text
30 FPS
```

必须解释为：

> **目标输出帧率设置**

不能在阶段 0 之前写成：

> “始终生成严格 30 CFR 文件”。

阶段 0 必须实测后再决定 v1 默认：

```text
IsFixedFramerate = false
或
IsFixedFramerate = true
```

## 15.2 G0 帧率验证方法

至少比较四种组合：

```text
A. Fixed=false + Fragmented=false
B. Fixed=true  + Fragmented=false
C. Fixed=false + Fragmented=true
D. Fixed=true  + Fragmented=true
```

测试素材至少包括：

```text
静态桌面 30 秒
持续动画 / 视频 30 秒
静态 → 动态 → 静态
```

分析：

- 总帧数
- sample PTS / duration
- `r_frame_rate`
- `avg_frame_rate`
- 是否存在明显非均匀帧间隔
- CPU / GPU / 文件大小差异
- A/V sync

开发测试可使用 MediaInfo / ffprobe 等分析工具，但不要求随产品发布。

## 15.3 Fragmented MP4

`IsFragmentedMp4Enabled` 当前确实存在，因此“提高异常退出时已写内容可恢复性”的方向保留。

但：

> **Fragmented MP4 不自动等于最终交付格式。**

G0 必须比较：

```text
普通 MP4
vs
Fragmented MP4
```

并验证：

- Windows 自带播放器可打开
- 至少一个常用第三方播放器可打开
- 至少一个实际目标剪辑软件可正常导入、拖动时间轴和导出
- 文件时长正确
- seek 正常
- 音画同步正常

如果 Fragmented MP4 在目标软件兼容性明显较差：

### 首选策略

```text
录制期间：
Fragmented MP4 / partial
        ↓
正常 Stop
        ↓
Finalize / Remux
        ↓
普通 MP4 成品
```

但是否引入 FFmpeg 只为 Remux，必须由 G0 结果决定。

原则：

- 不因为“理论上更兼容”就无条件加入 FFmpeg。
- 如果 ScreenRecorderLib 自身能安全得到兼容的普通 MP4，优先不增加依赖。
- Crash 后始终保留原始 fragmented / partial 文件，不覆盖原件。

---

# 16. 音频设计

## 15.1 录制与 Meter 解耦

```text
ScreenRecorderLib
   └─ 真正录音

NAudio
   └─ 只读取电平
```

## 15.2 设备 ID 必须绑定

Session 开始时把 Settings 中的：

```text
Default Device
```

解析为：

```text
Concrete Device ID
```

然后：

```text
Recorder Backend
+
NAudio Meter
```

都使用同一个 ID。

录制中默认设备改变：

- 不静默切换本 Session 的录制设备
- 由设备事件策略决定继续、提醒或停止

## 15.3 麦克风访问预检

Start 前尝试访问选定麦克风。

如果访问失败：

- 明确提示
- 允许用户选择“无麦克风继续”
- 不允许“音量条看似正常但成品静音”的无提示状态

---

# 17. DPI、多屏与 Capture Target 策略

## 16.1 PerMonitorV2 是强制配置

`app.manifest` 必须声明：

```xml
<dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">
  PerMonitorV2
</dpiAwareness>
```

实际 manifest 内容在阶段 0 建项目时确认。

## 16.2 Area Target

v1：

- 一个显示器一个 Selector Overlay
- 当前拖拽只能发生在单屏 Overlay 内
- 不允许跨屏 Area
- `AreaCaptureTarget` 带 `MonitorId`
- clip 到对应 Monitor Physical Bounds

## 16.3 Window Target 固定输出画布

编码期间不能动态改变 MP4 视频尺寸。

因此 Window Target 在 Start 时确定：

```text
Fixed Output Canvas
```

窗口后续 Resize 时，产品策略：

优先：

```text
窗口内容
↓
保持宽高比缩放
↓
放进固定 Canvas
↓
不足区域 padding / letterbox
```

如果 ScreenRecorderLib 当前窗口捕获路径无法支持这个策略，则阶段 0 G0-7 必须判断：

```text
A. 可实现动态缩放进固定 Canvas
B. 只能固定初始 capture rect
C. Resize 时只能安全 Stop
```

Spike 后再冻结最终行为。

## 16.4 Window Move / Minimize / Destroy

目标：

- Move：窗口模式应继续跟随 HWND，而不是依赖一次性初始 Rect
- Minimize：v1 默认安全停止，Reason=`TargetUnavailable`
- Destroy：立即安全停止，Reason=`TargetInvalidated`

是否允许“最小化继续录空白”不作为 v1 默认行为。

## 16.5 Monitor Target

开始时固定：

- MonitorId
- Physical Bounds
- OutputSize

录制期间显示器断开 / 分辨率改变：

> v1 选择安全停止，不做动态重建编码画布。

---

# 18. Overlay 与开始瞬间

## 17.1 Capture Exclusion

`WDA_EXCLUDEFROMCAPTURE` 用于：

- StatusWindow
- RecordingBorderWindow

要求它们都是当前进程顶级窗口。

## 17.2 在首次可见前设置排除

WPF 需要 HWND 后才能调用 Win32。

建议流程：

```text
创建 Window
↓
WindowInteropHelper.EnsureHandle()
↓
SetWindowDisplayAffinity(...)
↓
Show()
```

不能：

```text
Show()
↓ 用户先看到 / 捕获到一帧
↓
再设排除
```

## 17.3 开始捕获前先清理选择 UI

Selector / ControlBar 不应出现在成品第一帧。

开始流程：

```text
Hide / Close Selector
Hide / Close ControlBar
↓
等待至少一个 WPF Render / compositor cycle
↓
必要时 DwmFlush
↓
Backend.Start
```

StatusWindow / RecordingBorder：

- HWND 提前创建
- 提前设置 Exclusion
- Backend 成功后再显示

## 17.4 G0-5 必须测真实窗口形态

不能用普通不透明 Window 替代。

必须测最终：

```text
WindowStyle=None
AllowsTransparency=True（若最终方案需要）
Topmost=True
透明背景
click-through / no-activate（若最终需要）
```

和最终 ScreenRecorderLib 捕获路径的组合。

---

# 19. Windows 生命周期事件

## 18.1 Power Request 的边界

录制时使用 `SetThreadExecutionState` 防止**系统空闲计时器**自动休眠。

但它不能阻止：

- 用户主动点睡眠
- 合盖
- 电源键
- 某些系统 / 管理策略

因此不能把它当成“录制期间绝对不会睡眠”。

## 18.2 v1 事件策略

通过 Core：

```text
ISystemEventSource
```

接收 Infrastructure.Windows 转换后的事件。

### Session Lock

v1 默认：

```text
Session Locked
→ safe Stop
```

避免长时间录出不可控黑屏 / 锁屏内容。

### Suspend

```text
Suspend pending
→ best-effort safe Stop
```

Resume 后：

```text
不自动恢复旧 Session
```

### Shutdown / Logoff

```text
先原子更新 Session metadata
↓
在系统允许时间内 best-effort Stop
```

若来不及完整 Finalize，Recovery 接手。

### Display topology / Resolution / DPI Change

录制中：

```text
Monitor add/remove
Resolution change
DPI / topology change
→ safe Stop
```

v1 不在编码过程中重建 Canvas。

### GPU / Capture Device Loss

主要通过：

```text
Backend.Failed
```

统一进入 Stop。

### UAC / Secure Desktop

Windows Secure Desktop 不作为普通桌面捕获的一部分。

v1：

- 不尝试绕过
- 允许成品出现不可捕获 / 空白区间
- README 明确说明

---


# 20. 热键与完成通知

## 20.1 热键注册失败不是致命错误

v1 至少有：

```text
唤起 / 开始选区
停止录制
```

全局热键。

默认组合键在进入 Phase 1 时统一确定，但产品行为现在冻结：

1. 启动时尝试 `RegisterHotKey`。
2. 如果组合被其他程序占用：
   - 应用继续运行；
   - 设置页标记该热键“注册失败 / 已占用”；
   - 用户可以立即改键；
   - 不因热键失败导致录制功能不可用。
3. 不静默抢占其他应用热键。

## 20.2 完成通知

v1 不依赖复杂的系统 Toast 注册体系。

正常 Stop 后使用：

> **应用自有的轻量完成提示窗口**

显示：

```text
录制已保存
[打开文件夹]
```

特性：

- 不抢焦点；
- 数秒后自动消失；
- 用户点击“打开文件夹”时打开并选中最终文件；
- 若 Finalize / Recovery 有问题，改为明确错误提示，不显示“已保存”。

托盘菜单始终保留：

```text
打开录制文件夹
```

---

# 21. 项目结构与编译期边界

第一天拆项目，但必须按正确依赖方向。

```text
ScreenRecorder/
│
├─ src/
│  ├─ ScreenRecorder.Core/
│  │  ├─ ScreenRecorder.Core.csproj
│  │  ├─ State/
│  │  ├─ Recording/
│  │  ├─ Capture/
│  │  ├─ Sessions/
│  │  ├─ Storage/
│  │  ├─ Settings/
│  │  ├─ Models/
│  │  └─ Ports/
│  │     ├─ IRecordingBackend.cs
│  │     ├─ IRecordingBackendFactory.cs
│  │     ├─ IAudioMeterService.cs
│  │     ├─ IAudioDeviceService.cs
│  │     ├─ ISettingsStore.cs
│  │     ├─ IFileSystem.cs
│  │     ├─ IWindowQueryService.cs
│  │     ├─ IDisplayService.cs
│  │     ├─ ICaptureExclusionService.cs
│  │     ├─ IPowerRequestService.cs
│  │     └─ ISystemEventSource.cs
│  │
│  ├─ ScreenRecorder.Infrastructure.Windows/
│  │  ├─ Recording/
│  │  ├─ Audio/
│  │  ├─ Win32/
│  │  ├─ SystemEvents/
│  │  ├─ Storage/
│  │  └─ Logging/
│  │
│  ├─ ScreenRecorder.UI.Wpf/
│  │  ├─ RegionSelector/
│  │  ├─ ControlBar/
│  │  ├─ StatusWindow/
│  │  ├─ RecordingBorder/
│  │  ├─ Settings/
│  │  └─ Tray/
│  │
│  └─ ScreenRecorder.App/
│     ├─ App.xaml
│     ├─ Bootstrapper.cs
│     └─ app.manifest
│
├─ tests/
│  ├─ ScreenRecorder.Core.Tests/
│  └─ ScreenRecorder.IntegrationTests.Windows/
│
├─ docs/
│  ├─ architecture.md
│  ├─ technical-spike.md
│  └─ third-party-notices.md
│
├─ README.md
└─ ScreenRecorder.sln
```

## 19.1 Target Framework

建议：

```text
ScreenRecorder.Core
→ net8.0

UI / Infrastructure / App
→ net8.0-windows10.0.19041.0
```

Core 因此编译期无法使用：

- WPF
- Win32 UI
- ScreenRecorderLib
- NAudio

## 19.2 引用关系

```text
Core
↑  ↑
|  |
UI Infrastructure
 \ /
 App
```

具体：

```text
UI.Wpf → Core
Infrastructure.Windows → Core

App → Core
App → UI.Wpf
App → Infrastructure.Windows
```

禁止：

```text
Core → Infrastructure
UI → Infrastructure
```

## 19.3 托盘

WPF 没有原生 WPF Tray 控件。

可选：

- `System.Windows.Forms.NotifyIcon`
- 成熟 WPF Tray package

实现属于：

```text
UI.Wpf
```

App 只协调其生命周期，不把 Tray 库类型带进 Core。

---

# 22. 配置、日志与第三方依赖

## 20.1 Settings

```text
%AppData%/<ProductName>/settings.json
```

保存：

- 输出目录
- 分辨率策略
- 帧率
- 画质
- 系统输出设备偏好
- 麦克风设备偏好
- 默认声音开关
- 热键
- 磁盘阈值

## 20.2 Logging

抽象：

```text
Microsoft.Extensions.Logging
```

文件实现优先：

```text
Serilog
+
Serilog.Sinks.File
```

要求：

- 按天滚动
- 每条 Session 日志带 SessionId
- Start / Stop / BackendFailure / Recovery 可关联
- 不记录不必要的隐私信息

## 20.3 发布前 License Audit

发布前必须核对：

- ScreenRecorderLib
- NAudio
- Serilog / Tray package（若使用）
- FFmpeg（只有路线 C）
- 其他 NuGet / native binaries

生成：

```text
THIRD-PARTY-NOTICES
```

当前已知：

- ScreenRecorderLib：MIT
- NAudio：MIT
- FFmpeg：基础为 LGPL 2.1+，若构建启用 GPL 组件则整体适用 GPL 条款

路线 C 的 FFmpeg 二进制必须明确：

- 来源
- build configuration
- 是否包含 GPL components
- 分发义务

---

# 23. 阶段 0：技术 Spike

阶段 0 调整为：

> **2～3 天**

目标不是做完整产品，而是把会决定架构和 Backend 合约的风险先测掉。

## 23.2 G0-1：Area Capture

验证：

- 单显示器区域
- MP4 输出
- Physical Pixel rect
- 偶数尺寸处理

## 23.3 G0-2：System Audio

验证系统声音稳定写入。

## 23.4 G0-3：System Audio + Microphone

验证：

- 双路同时存在
- 设备 ID 可明确选择
- 没有明显同步漂移

## 23.5 G0-4：Fragmented MP4 / Crash

测试：

- 正常 Stop
- 强制进程结束
- 残留 partial 文件
- 实际可播放 / 可恢复程度
- 普通 MP4 与 Fragmented MP4 的播放器兼容性
- 至少一个实际目标剪辑软件的导入 / seek / 导出兼容性
- 是否需要正常 Stop 后把 fragmented 成品转封装为普通 MP4

同时把本节与 §15 的 CFR / VFR 组合测试合并执行，避免重复录制。

## 23.6 G0-5：最终 Overlay Capture Exclusion

使用真实：

- StatusWindow
- RecordingBorderWindow
- `AllowsTransparency` 最终属性
- click-through / no-activate 最终属性

流程：

```text
EnsureHandle
→ WDA_EXCLUDEFROMCAPTURE
→ Show
→ 真正 ScreenRecorderLib capture
```

只有“最终窗口形态 + 最终捕获路径”同时通过才算 PASS。

并且 G0-5 不是只跑一次：

```text
Area 最终捕获路径
Window / WGC 路径
Monitor 最终捕获路径
```

凡是 v1 会使用的实际 capture API，都必须分别验证 Overlay 排除。

如果同一个 Target 存在 Desktop Duplication / WGC 两个候选实现，则阶段 0 可以比较后只保留一个正式路径，避免产品运行时维护两套不必要的行为差异。

## 23.7 G0-6：30～60 分钟稳定性

观察：

- CPU
- GPU
- Working Set
- Private Bytes
- 文件持续增长
- A/V sync
- Stop 耗时

额外记录：

> 正常 Stop P50 / P95 时间，用于确定正式 Stop Timeout。

## 23.8 G0-7：Window / Monitor / DPI 冒烟

这是本轮新增的必要 Gate。

### Window / WGC

当前库实现把 WindowRecordingSource 绑定到 Windows Graphics Capture，因此这里必须单独作为 WGC 路径测试。

测试：

- 选择窗口
- 移动窗口
- Resize
- Minimize
- Restore
- Destroy

记录：

- 是否真按 HWND 跟随
- Resize 后输出如何处理
- Minimize 的底层行为

### Monitor / Area capture API

Display Source 分别测试当前候选：

```text
Desktop Duplication
Windows Graphics Capture```

然后基于：

- Capture Exclusion
- cursor
- 性能
- 多屏 / 负坐标
- 稳定性

选择 v1 正式路径。

测试：

- 主屏
- 副屏
- 多屏

### DPI / 坐标

至少：

```text
100% + 150%
副屏在主屏左侧（负 X）
副屏在主屏上方（负 Y）
```

验证：

- 选区与实际画面一致
- Window hit test 一致
- Monitor bounds 一致

## 23.9 Spike 结论

每个 Gate：

```text
PASS
PASS WITH WORKAROUND
FAIL
```

输出：

```text
docs/technical-spike.md
```

阶段 0 完成后才冻结：

- IRecordingBackend 的最终方法形状
- Window resize 行为
- Backend stop timeout
- ScreenRecorderLib 路线 A / B / C
- Overlay 最终窗口实现

---

# 24. 开发阶段与工期

| 阶段 | 内容 | 工期 |
| --- | --- | --- |
| 0 技术 Spike | 多项目骨架、G0-0～G0-7、干净 VM、CFR/VFR、媒体兼容、短时稳定性、Spike 报告 | 2～3 天 |
| 1 核心录制 MVP | Session、Backend Factory、状态机、Preflight、Start/Stop、手动区域、声音、托盘、热键 | 3–4 天 |
| 2 捕获目标 | Window / Monitor、单屏 Area、多屏与 DPI | 2–3 天 |
| 3 Overlay | Status、Border、文件大小、双 Meter、Capture Exclusion | 2 天 |
| 4 可靠性 | StorageGuard、Power、系统事件、Recovery、Stop Timeout、8h 压测 | 3–4 天 |
| 5 设置 | 分辨率、帧率、画质、目录、设备、热键 | 1–2 天 |
| 6 发布 | Self-contained、安装 / Portable、README、License Audit、测试收尾 | 2–3 天 |

预计：

> **15～21 个工作日，按每天几小时建议预留约 3～4 周。**

不包含：

- 完整 WGC + FFmpeg 重写
- 自动 MP4 Repair / Remux
- Pause / Resume

如果阶段 0 进入路线 C：

> **重新估算，原 3～4 周基线失效。**

---

# 25. 阶段实施细节

## Phase 0

第一天就建立正确的项目引用方向。

不冻结接口签名，先做 Spike。

## Phase 1

完成：

- 建立 `ScreenRecorder.Core.Tests`
- AppStateMachine 唯一真源
- SessionManager
- Backend Factory
- Backend Failed 回传
- Preflight
- 同卷 Session 路径
- 原子 session.json
- Start / Stop 串行化
- Pending Stop During Starting
- Stop Timeout / Faulted 机制的 Fake Backend 单测
- 状态机所有合法 / 非法转移单测
- Starting 中 PendingStop 单测
- 多入口并发 Stop 幂等单测
- StartFailed → Ready / Idle 单测
- Recovery 对 Created / Recording / StopFailed / Orphaned Session 的单测
- `session.json` 原子更新失败注入测试
- 手动单屏 Area
- Tray / Hotkey

## Phase 2

完成：

- Window hit test
- Window target policy
- Monitor selection
- PerMonitorV2
- 负坐标
- 混合 DPI
- Display event baseline

## Phase 3

完成：

- StatusWindow
- RecordingBorderWindow
- EnsureHandle → Exclusion → Show
- 双音量条
- 文件大小

## Phase 4

完成：

- StorageGuard
- SetThreadExecutionState
- Session lock
- Suspend / Resume
- Shutdown / Logoff
- Display topology change
- audio hot-plug
- Recovery
- 8 小时压测

## Phase 5

高级设置。

## Phase 6

发布、文档、License Audit。

---

# 26. 测试矩阵

## 显示 / DPI

- 单屏 100%
- 双屏 100% + 100%
- 双屏 100% + 150%
- 副屏负 X
- 副屏负 Y
- 分辨率变化
- 显示器热插拔
- v1 跨屏 Area 被正确禁止

## Window Target

- Move
- Resize
- Minimize
- Restore
- Destroy
- Window 被移到另一显示器

## Audio

- System only
- Microphone only
- Both
- 默认设备改变
- 蓝牙设备切换
- 麦克风拔出
- 麦克风访问不可用 / 隐私设置阻止
- Meter 和 Recorder 使用同一 concrete device ID

## Lifecycle

- 快速双击 Start
- Starting 中按 Stop
- Start 初始化失败
- Backend.Failed
- 多入口同时 Stop
- Stop fake backend 永不返回
- Dispose fake backend 永不返回
- UI Window 被意外关闭
- App 正常退出
- 进程强制结束

## System Events

- Session Lock
- Suspend / Resume
- Shutdown / Logoff（可测试范围内）
- Display topology change
- Graphics / capture device loss（可复现范围内）
- UAC / Secure Desktop 说明验证

## Storage

- 正常
- 低空间
- 写满
- 无权限
- 输出盘移除
- final 与 partial 同卷
- session.json 中途写入异常

## Media / Container

- Fixed=false / true
- Fragmented=false / true
- 静态内容 / 动态内容
- 媒体帧率 / PTS 分析
- Windows 自带播放器
- 至少一个第三方播放器
- 至少一个实际目标剪辑软件

## Hotkey / Notification

- 默认热键注册成功
- 热键被占用
- 改键后重新注册
- Stop 后完成提示
- Finalize 失败时不显示“已保存”

## Deployment

- 干净 Win10 VM
- 干净 Win11 VM
- VC++ Runtime 缺失
- Folder self-contained
- Installer
- Portable
- Single-file 兼容性实验

## Long Run

- 30 分钟
- 1 小时
- 8 小时（按 §27 量化指标验收）

---

# 27. 性能与可靠性指标

阶段 0 后用实测校准。

### 8 小时测试不是“没崩就算过”

必须记录至少：

```text
CPU average / peak
GPU utilization
Working Set
Private Bytes
文件大小增长
帧数 / 帧率统计
Start A/V offset
End A/V offset
Stop / Finalize duration
```

初始验收判据：

- 全程无 crash / hang / unhandled exception；
- 输出文件能被至少两个播放器完整打开并 seek 到结尾；
- 从 30 分钟 warm-up 后观察，Private Bytes 不应呈持续无界线性增长；
- 若 1h→8h Private Bytes 增长超过 **1 GiB** 或最后 4 小时平均增长超过 **100 MiB/h**，判为失败并调查；
- 8 小时结束时相对开始的**累计 A/V drift ≤ 150 ms**；
- 没有连续 ≥ 2 秒的意外视频冻结或音频缺失；
- 正常 Stop / Finalize 必须在正式 Stop Timeout 内完成；
- Finalize 后媒体时长与实际录制时长误差 ≤ 1 秒或 ≤ 0.1%（取较大者）。

其他目标：

- UI 卡顿不影响编码线程
- 多入口 Stop 不重复 finalize
- Stop 不无限等待
- 非空 partial 媒体不被静默删除
- Window / Monitor 目标行为与文档一致
- 首帧不包含 Selector / ControlBar
- 被排除 Overlay 不进入视频

---

# 28. 风险与应对

| 风险 | 应对 |
| --- | --- |
| ScreenRecorderLib 核心能力不足 | G0 Gate 后路线 A / B / C |
| Backend Stop 卡死 | Timeout → StopFailed → best-effort dispose → Faulted |
| Session 临时文件跨卷 | 强制 WorkingDirectory 与 FinalOutput 同卷 |
| session.json 写坏 | temp + atomic replace / rename |
| Overlay 排除行为不一致 | G0-5 测最终透明窗口 |
| Window resize 语义不清 | G0-7 决定固定 Canvas 策略 |
| 混合 DPI | PerMonitorV2 + Core Physical Pixel |
| Display topology 改变 | v1 录制中安全 Stop |
| 手动睡眠 / 合盖 | Power event best-effort Stop；不依赖 SetThreadExecutionState 保证 |
| GPU reset | Backend.Failed → Stop |
| UAC Secure Desktop | 系统限制，README 说明 |
| Mic access 被阻止 | Start Preflight + 明确提示 |
| 默认音频设备切换 | Session 固定 concrete device ID |
| 旧版 Windows | 不支持 Win10 2004 以下 |
| VC++ Runtime 缺失 | Installer 检测 / 安装；干净 VM Gate |
| Single-file 与 C++/CLI 不兼容 | 不作为 v1 必达；Folder publish 为默认 |
| 默认非 fixed framerate | G0 比较 CFR / 非固定帧率；UI 用“目标帧率”语义 |
| Fragmented MP4 编辑兼容 | G0 播放器 + 编辑器测试；必要时正常 Stop 后 remux |
| Window 强制走 WGC | Window 路径与 Area/Monitor 路径分别验收 |
| System+Mic 回声 | v1 无 AEC；建议耳机并写入已知限制 |
| FAT32 4GB 限制 | Preflight 拒绝 FAT32 作为输出卷 |
| SmartScreen / app reputation | 公共发布优先代码签名；不承诺零警告 |
| FFmpeg License | 路线 C 发布前核对 build / LGPL / GPL |
| 范围膨胀 | P0/P1/P2 严格执行 |

---

# 29. v1 验收标准

## Core

- [ ] UI → Core ← Infrastructure 依赖方向无循环
- [ ] Core 为纯 `net8.0`
- [ ] UI 不引用 Infrastructure
- [ ] AppStateMachine 是 AppState 唯一真源
- [ ] Session 状态不可被 UI public set

## Capture

- [ ] 单屏 Area 正常
- [ ] Window 正常
- [ ] Monitor 正常
- [ ] PerMonitorV2
- [ ] 100% + 150% 正常
- [ ] 负坐标正常
- [ ] v1 禁止跨屏 Area

## Audio

- [ ] 双路录音正常
- [ ] concrete device ID 固定
- [ ] Meter 与 Recorder 同设备
- [ ] Mic 无法访问时有明确提示

## Lifecycle

- [ ] Starting 中 Stop 行为确定
- [ ] Start failed 返回 Ready / Idle 规则确定
- [ ] Stop 幂等
- [ ] Stop 有 Timeout
- [ ] 无法确认 Backend 静止时进入 Faulted
- [ ] UI Window 关闭不停止 Recorder

## File Safety

- [ ] partial 与 final 同卷
- [ ] session.json 原子更新
- [ ] 正常 finalize 不需要跨卷大文件复制
- [ ] 残留 Session 可识别
- [ ] 非空 partial 不被自动删除

## Windows Events

- [ ] Lock 策略验证
- [ ] Suspend / Resume 策略验证
- [ ] Display change 策略验证
- [ ] Shutdown / Logoff best-effort 路径存在
- [ ] UAC / Secure Desktop 写入已知限制

## UI

- [ ] 三步开始
- [ ] Selector / ControlBar 不出现在首帧
- [ ] Status / Border 不进入成品
- [ ] 双 Meter 正常
- [ ] 文件大小刷新正常

## Release

- [ ] 干净 Win10 / Win11 VM 可从最终发布物完成录制
- [ ] 安装器检测 / 安装 VC++ Redistributable
- [ ] Folder self-contained 通过
- [ ] Single-file 若提供则单独通过兼容性测试
- [ ] FAT32 输出被明确阻止
- [ ] 文件命名不会静默覆盖
- [ ] README
- [ ] 日志
- [ ] THIRD-PARTY-NOTICES
- [ ] License Audit 完成
- [ ] 公共发布已评估代码签名 / SmartScreen 策略

---

# 30. 发布与后续

## 30.1 发布候选必须通过干净 VM

至少验证：

```text
Windows 10 22H2 x64 clean VM
Windows 11 supported x64 clean VM
```

目标：

- 无需预装 .NET Runtime；
- 安装器正确处理 VC++ Redistributable；
- ScreenRecorderLib 能加载；
- 可完成 Area / Window / Monitor 基本录像；
- Stop 后成品可播放；
- 卸载后不误删用户录像。

Portable 版本必须明确：

- 是否自带 / 依赖 VC++ Runtime；
- 缺依赖时如何提示。

## 30.2 Code Signing / SmartScreen

“屏幕捕获 + 全局热键”本身不等于恶意软件，但新发布、低信誉或未签名的 Win32 二进制可能触发 SmartScreen / Smart App Control 警告。

因此：

- 本地开发 / 内测可以不签名；
- 面向公众正式发布时，**代码签名列为发布建议项，优先完成**；
- Installer 与主 EXE 使用一致发布者身份签名；
- 不把“签名后绝不出现 SmartScreen”作为承诺；
- 如预算 / 身份条件暂时无法签名，README 必须明确发布来源和校验方式。

## 30.3 后续能力

v1 稳定后再考虑：

- 选区记忆
- 倒计时
- Pause / Resume
- 鼠标点击高亮
- GIF
- 简易裁剪
- 录制历史
- 分享
- 自动 Repair / Remux
- Backend capability auto-detection

---

# 31. 冻结策略

## 29.1 现在冻结什么

v1.2 冻结：

```text
UI → Core ← Infrastructure
App = Composition Root
Core 不依赖 WPF / ScreenRecorderLib / NAudio
平台能力接口定义在 Core
Backend 每 Session 新建
AppState 唯一真源
Session 媒体安全规则
同卷工作目录
原子 metadata
Stop 必须 bounded
PerMonitorV2
Area v1 单显示器
Windows 10 2004+ 技术最低版本
```

## 29.2 现在不冻结什么

阶段 0 前**不冻结**：

```text
IRecordingBackend 的最终方法签名
IRecordingBackendFactory 的最终返回形式
ScreenRecorderLib 具体 Options 映射
IsFixedFramerate 默认值
Fragmented MP4 是否直接作为最终成品
是否需要正常 Stop 后 Remux
Area / Monitor 最终选择 Desktop Duplication 还是 WGC
Window Resize 最终实现
Stop Timeout 最终秒数
Overlay 是否必须 AllowsTransparency
路线 A / B / C 最终选择
Single-file 是否提供
```

这些必须让 Spike 说话。

## 29.3 阶段 0 后

完成 G0-1～G0-7 后：

1. 修订 technical-spike.md
2. 锁定 Backend contract
3. 锁定窗口 / 显示器策略
4. 锁定 Stop timeout
5. 锁定技术路线
6. 生成真正的“架构冻结版”

---

# 32. 本轮审查意见处理结论

| 建议 | 判断 | 处理 |
| --- | --- | --- |
| Backend 等接口放 Core，修复循环依赖 | **完全采纳** | 改为 UI → Core ← Infrastructure |
| UI 需要 Win32 能力 | **完全采纳** | UI 通过 Core Port 使用，不直接 P/Invoke |
| 第三方只能在 Infrastructure 太死 | **完全采纳** | 改为第三方按所属层隔离，不泄漏类型 |
| 状态多个主人 | **完全采纳** | AppStateMachine 唯一真源；SessionStatus 仅持久化 |
| Session public set | **完全采纳** | 改 private set / Core 内部方法 |
| Start 前 Session 创建顺序错误 | **完全采纳** | Preflight 先于 Session 创建 |
| Starting 中 Stop 未定义 | **完全采纳** | PendingStopReason，Start 明确结束后立即 Stop |
| Start failure 返回状态 | **完全采纳** | Target 有效→Ready；无效→Idle |
| Stop 无超时 | **采纳核心观点** | 增加 Timeout；不承诺危险的线程强杀 |
| Stop 超时后强制释放 | **部分采纳** | best-effort cancel/dispose；无法确认则 Faulted |
| Session 与 final 同卷 | **完全采纳** | working dir 放输出卷 |
| session.json 原子更新 | **完全采纳** | temp + replace/rename |
| Backend 每 Session 新建 | **完全采纳** | 增加 Core factory port |
| 锁屏 / 睡眠 / 显示事件 | **完全采纳** | 增加 ISystemEventSource + v1 策略 |
| Window resize 固定输出尺寸 | **完全采纳** | 固定 Canvas；G0-7 决定具体缩放能力 |
| Area v1 限单显示器 | **完全采纳** | Target 增 MonitorId；禁止跨屏 |
| PerMonitorV2 manifest | **完全采纳** | 成为项目初始化项 |
| Mic privacy / access | **采纳** | Start Preflight 检测并提示 |
| Meter 与录音设备一致 | **完全采纳** | Session 固定 concrete device ID |
| 开始前移除 UI / 等一帧 | **完全采纳** | Hide → render/compositor cycle → Start |
| Exclusion 在窗口显示前设 | **采纳并修正实现** | EnsureHandle → SetAffinity → Show |
| G0-7 Window/Monitor/DPI | **完全采纳** | 新增 Gate |
| Stage 0 原建议改 2 天 | **采纳并随新增 Gate 调整** | 最终改为 2～3 天 |
| 先只冻结规则，不锁签名 | **完全采纳** | v1.2 定位为冻结前工程基线 |
| 最低 Win10 2004+ | **完全采纳** | 删除旧系统降级分支 |
| 工期范围继续上调 | **采纳** | 15～20 工作日 / 约 3～4 周 |
| 文件日志 Provider | **完全采纳** | 明确 Serilog 等具体 Sink |
| 路线 B 模糊 | **完全采纳** | 删除“WGC 辅助”，定义为平台 workaround |
| 发布前许可证检查 | **完全采纳** | THIRD-PARTY-NOTICES + audit |
| RecordingOptions 路径归属混乱 | **完全采纳** | 路径归 SessionManager；Options 增 OutputSize / DeviceId |
| VC++ Redistributable 依赖 | **完全采纳** | 增 G0-0 干净 VM Gate；Installer 检测 / 安装 |
| Single-file 风险 | **采纳为验证项** | 不作为 v1 必达；默认 Folder self-contained |
| 默认可变 / 非固定帧率 | **完全采纳** | G0 对比 fixed=false/true；UI 改为“目标帧率”语义 |
| Window 走 WGC | **完全采纳** | Window 与 Area/Monitor 分路径验收 |
| Fragmented MP4 编辑兼容 | **完全采纳** | G0 播放器 + 编辑器；Remux 由测试决定 |
| System + Mic 音轨定义 | **采纳** | v1 暂定混合单轨，并由 G0 媒体分析确认 |
| 扬声器回声 | **完全采纳为已知限制** | v1 不做 AEC，建议耳机 |
| 热键冲突 | **完全采纳** | RegisterHotKey 失败不致命，设置页提示改键 |
| Stop 完成提示 | **采纳** | 自有轻量完成提示窗口，不强绑系统 Toast |
| 文件命名 / 重名 | **完全采纳** | 时间戳 + `_NNN`；禁止覆盖 |
| FAT32 | **完全采纳** | Start Preflight 直接拒绝 |
| AppData `<ProductName>` | **采纳** | 开发期内部 AppId 固定为 `ScreenRecorder` |
| 日志窗口标题隐私 | **完全采纳** | 默认禁止记录 Window Title |
| Core 单元测试 | **完全采纳** | FakeBackend 覆盖状态机、并发 Stop、Recovery |
| 8h 量化标准 | **完全采纳** | 增内存、A/V drift、Finalize、可播放阈值 |
| Code Signing | **采纳并修正理由** | 不是“捕获必然触发杀软”；公共发布优先签名以改善信誉体验 |

---

# 33. 下一步

阶段 0 的执行顺序固定为：

```text
1. 建 Solution
2. 建 Core / Infrastructure.Windows / UI.Wpf / App
3. 配置正确项目引用
4. 配 app.manifest / PerMonitorV2
5. 建 Core Ports 的最小示意接口
6. 实现 ScreenRecorderLib Spike Backend
7. 实现真实 StatusWindow / RecordingBorderWindow
8. 先跑 G0-0 干净 VM / VC++ Runtime 加载验证
9. 跑 G0-1 ～ G0-7
10. 比较 fixed / fragmented 四组合，并分析媒体时间戳
11. 分别验证 Area/Monitor 与 Window/WGC 的 Capture Exclusion
12. 记录 Stop 时间、Window resize、设备、DPI、Exclusion、媒体兼容结果
13. 输出 technical-spike.md
14. 再冻结接口签名与成品 MP4 策略
15. 进入 Phase 1
```

阶段 0 前不继续增加 P2 功能。

---

# 34. 关键外部依据

- Microsoft `SetWindowDisplayAffinity`：  
  https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity

- Microsoft `SetThreadExecutionState`：  
  https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate

- Microsoft Application Manifest / PerMonitorV2：  
  https://learn.microsoft.com/windows/win32/sbscs/application-manifests

- ScreenRecorderLib：  
  https://github.com/sskodje/ScreenRecorderLib

- ScreenRecorderLib License：  
  https://github.com/sskodje/ScreenRecorderLib/blob/master/LICENSE

- NAudio：  
  https://github.com/naudio/NAudio

- FFmpeg License / Legal：  
  https://ffmpeg.org/legal.html

- .NET Single-file deployment：  
  https://learn.microsoft.com/dotnet/core/deploying/single-file/overview

- Microsoft SmartScreen reputation for Windows app developers：  
  https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation