# 🎯 File Encoding Convert Tool

![Avalonia Logo](Assets/avalonia-logo.ico)  
*A cross-platform file encoding conversion tool built with Avalonia*

---

## ✨ 功能特点

- 🔍 **智能检测** - 支持多种编码格式检测(UTF-8, GBK, Unicode等)
- 🔄 **批量转换** - 高效处理多个文件的编码转换
- 🖥️ **直观界面** - 简洁易用的图形用户界面
- 🌐 **跨平台** - 支持Windows/Linux/macOS系统
- 📊 **实时进度** - 转换进度清晰可见

---

## 🚀 快速开始

### 系统要求
- [.NET 7.0](https://dotnet.microsoft.com/)或更高版本

### 安装使用
1. 克隆仓库或下载发布版本
   ```bash
   git clone https://github.com/your-repo/FileEncodingConvertTool.git
   ```
2. 构建并运行项目
   ```bash
   dotnet run
   ```

---

## 🖼️ 界面预览
*(截图位置 - 主界面展示)*

---

## 🛠️ 使用说明

主界面包含三个主要区域：

| 区域 | 功能 |
|------|------|
| 📂 文件选择区 | 添加/移除待转换文件 |
| ⚙️ 编码设置区 | 选择源编码和目标编码 |
| ▶️ 操作区 | 开始转换/停止/清空列表 |

---

## 💻 代码示例

### 检测文件编码
```csharp
// 使用EncodingDetectorUtil检测文件编码
var encoding = EncodingDetectorUtil.DetectFileEncoding("test.txt");
Console.WriteLine($"Detected encoding: {encoding.EncodingName}");
```

### 转换文件编码
```csharp
// 使用FileEncodingCovert转换文件编码
FileEncodingCovert.ConvertFileEncoding(
    "source.txt", 
    "target.txt", 
    Encoding.UTF8, 
    Encoding.GBK,
    progress => Console.WriteLine($"Progress: {progress}%"));
```

---

## 📂 项目结构

```text
FileEncodingConvertTool/
├── Models/          # 数据模型
│   └── FileEncodingData.cs
├── Services/        # 服务层
│   └── ProgressManager.cs
├── Utils/           # 工具类
│   ├── EncodingDetectorUtil.cs
│   ├── FileEncodingCovert.cs
│   └── JsonPersister.cs
├── ViewModels/      # 视图模型
│   ├── MainWindowViewModel.cs
│   └── ...
├── Views/           # 视图界面
│   ├── MainWindow.axaml
│   └── ...
├── App.axaml        # 主应用定义
└── Program.cs       # 程序入口
```

---

## 📜 许可证

本项目采用 [MIT License](LICENSE) 开源协议。

---

## 🤝 贡献指南

欢迎提交Pull Request或Issue报告问题！
