# ObiRopeInteractable Component

## 概述

`ObiRopeInteractable` 是一個專為Obi繩子與forceps交互而設計的模塊化組件。它實現了當繩子進入trigger zone後，grip鉗子時繩子最近的粒子會被吸附到最近的attach point的邏輯。

## 特性

- ✅ **自動檢測**: 自動檢測附近的forceps並監控grip狀態
- ✅ **粒子吸附**: 將最近的繩子粒子吸附到最近的attach point
- ✅ **多粒子附著**: 支持同時附著多個連續粒子以提供更好的控制
- ✅ **運動學/動力學模式**: 支持kinematic(穩定)和dynamic(物理力)附著模式
- ✅ **平滑過渡**: 可選的平滑附著過渡動畫
- ✅ **模塊化設計**: 易於集成到現有項目中
- ✅ **調試支持**: 豐富的調試信息和可視化gizmos

## 安裝步驟

### 1. 設置繩子對象
```csharp
// 在繩子GameObject上添加ObiRopeInteractable組件
GameObject ropeObject; // 必須有ObiActor組件
ropeObject.AddComponent<ObiRopeInteractable>();

// 確保繩子有正確的標籤
ropeObject.tag = "Rope";
```

### 2. 設置Forceps對象
```csharp
// 確保forceps有必要的組件
ForcepsController forcepsController; // 已存在
RopeXRDirectInteractor ropeInteractor = forcepsObject.AddComponent<RopeXRDirectInteractor>();

// 設置attach points
ropeInteractor.SetAttachPoints(upperClamp, lowerClamp);
```

### 3. 自動設置(推薦)
使用 `RopeInteractionExample` 腳本的自動設置功能：

1. 將 `RopeInteractionExample` 添加到場景中的任意GameObject
2. 在Inspector中分配rope和forceps對象(可選，會自動尋找)
3. 點擊 "Setup Rope Interaction" 按鈕或在運行時自動設置

## 配置選項

### 交互設置
- **Enable Interaction**: 是否啟用繩子交互
- **Detection Radius**: 檢測附近forceps的半徑
- **Attach Particle Count**: 要附著的連續粒子數量
- **Max Attach Distance**: 考慮附著的最大距離

### 附著行為
- **Use Kinematic Attach**: 使用運動學附著(更穩定)
- **Attach Force Multiplier**: 動力學模式的力倍數
- **Smooth Attachment**: 啟用平滑附著過渡
- **Attachment Duration**: 附著過渡持續時間

### 調試設置
- **Show Debug Info**: 顯示調試信息
- **Show Debug Gizmos**: 顯示可視化調試gizmos

## 使用方法

### 基本使用
1. 確保繩子進入forceps的trigger zone
2. 按下grip按鈕
3. 繩子的最近粒子會自動吸附到最近的attach point
4. 釋放grip按鈕解除附著

### 程式化控制
```csharp
// 獲取組件引用
ObiRopeInteractable ropeInteractable = rope.GetComponent<ObiRopeInteractable>();

// 檢查狀態
bool isAttached = ropeInteractable.IsAttached;
int attachmentCount = ropeInteractable.GetAttachmentCount();

// 控制交互
ropeInteractable.EnableInteraction = true;
ropeInteractable.DetectionRadius = 0.03f;

// 強制解除所有附著
ropeInteractable.DetachAll();

// 檢查是否附著到特定forceps
bool attachedToForceps = ropeInteractable.IsAttachedTo(specificForceps);
```

## 技術實現

### 組件依賴
- `ObiActor`: 必需，提供繩子粒子數據
- `ForcepsController`: 檢測grip狀態和trigger zone
- `RopeXRDirectInteractor`: 提供attach points位置

### 交互流程
1. **檢測階段**: 每幀檢測附近的forceps
2. **觸發條件**: grip按下 + 繩子在trigger zone + 未附著
3. **附著計算**: 找到最近的粒子和attach point
4. **粒子操作**: 設置kinematic或應用力
5. **位置更新**: 持續更新附著粒子位置
6. **解除條件**: grip釋放或手動解除

### 性能優化
- 緩存組件引用避免重複查找
- 使用反射安全地訪問私有字段
- 限制調試輸出頻率
- 高效的距離計算

## 調試技巧

### 可視化調試
- 在Scene視圖中選中繩子對象查看檢測範圍
- 紅色線條表示活動的附著連接
- 青色圓圈表示粒子檢測範圍

### 控制台信息
```
ObiRopeInteractable (RopeName): Found 2 nearby forceps
Attached rope RopeName to forceps ForcepsName with 3 particles
Detached rope RopeName from forceps ForcepsName
```

### 測試方法
使用 `RopeInteractionExample` 腳本的右鍵選單：
- "Test - Enable/Disable Rope Interaction"
- "Test - Detach All"
- "Test - Adjust Detection Radius"

## 常見問題

### Q: 繩子不會附著到forceps
A: 檢查以下幾點：
- 繩子是否有 `ObiRopeInteractable` 組件
- 繩子標籤是否為 "Rope"
- Forceps是否有 `RopeXRDirectInteractor` 組件
- 檢測半徑是否足夠大
- Forceps的grip功能是否正常

### Q: 附著不穩定或抖動
A: 嘗試以下解決方案：
- 啟用 "Use Kinematic Attach"
- 增加 "Attach Particle Count"
- 啟用 "Smooth Attachment"
- 調整 "Attachment Duration"

### Q: 性能問題
A: 優化建議：
- 減少檢測半徑
- 降低附著粒子數量
- 關閉不必要的調試信息
- 限制場景中的繩子數量

## 擴展和自定義

### 添加自定義附著邏輯
```csharp
public class CustomRopeInteractable : ObiRopeInteractable
{
    protected override void ProcessAttachmentLogic()
    {
        // 自定義附著邏輯
        base.ProcessAttachmentLogic();
    }
}
```

### 集成其他交互系統
組件設計為模塊化，可以輕鬆與其他VR交互系統集成，只需實現相應的接口即可。

## 版本兼容性

- Unity 2021.3 LTS+
- Obi Rope 6.x
- XR Interaction Toolkit 2.x

## 許可證

按照項目主許可證使用。
