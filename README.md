## 本地部署语言模型+2D解谜战斗游戏（Unity部分）

该项目使用 Unity 开发游戏原型，包含角色控制、战斗系统、敌人状态机、调用推理服务与剧情分支等核心功能。游戏中的谜题 NPC 由本地运行的开源语言模型支持，能够理解玩家以自然语言提出的问题，并以结构化结果返回“是”“否”“无关”或“接近真相”等回答。

# 战斗系统
采用类似《空洞骑士》战斗风格进行设计。

<img src="https://github.com/Wang-Yuan0813/2D-Action-Game-in-Unity/raw/main/gifs/boss1.gif" width="500">

当玩家处于有效招架窗口、攻击来自玩家面对方向，并且敌方攻击被标记为可招架近战攻击时，系统将此次碰撞判断为成功招架。成功招架会取消敌人的当前攻击窗口，为玩家提供短时间保护，并恢复玩家能量。

<img src="https://github.com/Wang-Yuan0813/2D-Action-Game-in-Unity/raw/main/gifs/parry.png" width="500">

激光弹幕攻击采用对象池进行实现

<img src="https://github.com/Wang-Yuan0813/2D-Action-Game-in-Unity/raw/main/gifs/boss2.gif" width="500">

# NPC交互

在本项目中NPC交互分为两种，一是使用预设对话树的传统交互，二是使用本地推理服务的自然语言交互。

<img src="https://github.com/Wang-Yuan0813/2D-Action-Game-in-Unity/raw/main/gifs/normalNPC.gif" width="500">

<img src="https://github.com/Wang-Yuan0813/2D-Action-Game-in-Unity/raw/main/gifs/questions.gif" width="500">

采用海龟汤作为解谜机制，即NPC只能回复“是”，“否”，“无法分辨”和“与题目无关”。

# 推理服务

本地推理服务通过使用Qwen3-1.7B作为基础模型，并训练两个LoRA适配器实现对应回复功能。总体架构如下：

<img src="https://github.com/Wang-Yuan0813/2D-Action-Game-in-Unity/raw/main/gifs/structure.png" width="500">

# 游戏流程

玩家需要通过海龟汤机制解谜来解锁最终剧情，否则将在最后推论提交后进入分支替代剧情，分支替代剧情结束后将会返回开始阶段重新进行。

<img src="https://github.com/Wang-Yuan0813/2D-Action-Game-in-Unity/raw/main/gifs/gameflow.png" width="500">
