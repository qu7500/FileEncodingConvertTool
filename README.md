# File Encoding Convert Tool

一个基于Avalonia框架开发的跨平台文件编码转换工具，支持多种编码格式的检测和转换。

## 功能特点

- 支持多种编码格式检测(UTF-8, GBK, Unicode等)
- 批量文件编码转换功能
- 直观的图形用户界面
- 跨平台支持(Windows/Linux/macOS)
- 转换进度实时显示

## 使用说明

1. 下载并安装.NET 7.0或更高版本
2. 克隆本项目或下载发布版本
3. 运行程序后，主界面包含以下功能区域：
   - 文件选择区：添加/移除待转换文件
   - 编码设置区：选择源编码和目标编码
   - 操作区：开始转换/停止/清空列表

## 示例代码

```csharp
// 检测文件编码
var encoding = EncodingDetectorUtil.DetectFileEncoding("test.txt");

// 转换文件编码
FileEncodingCovert.ConvertFileEncoding(
    "source.txt", 
    "target.txt", 
    Encoding.UTF8, 
    Encoding.GBK);
```

## 项目结构

```
FileEncodingConvertTool/
├── Models/          # 数据模型
├── Services/        # 服务层
├── Utils/           # 工具类
├── ViewModels/      # 视图模型
├── Views/           # 视图界面
├── App.axaml        # 主应用定义
└── Program.cs       # 程序入口
```

## 许可证

本项目采用 [MIT License](LICENSE) 开源协议。
