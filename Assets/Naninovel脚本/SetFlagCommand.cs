using Naninovel;
using Command = Naninovel.Command;

/// <summary>
/// 自定义 Naninovel 命令：@setFlag
/// 在 .nani 脚本中设置 AdventureFlag，用于奇遇选择后果的跨Run持久化。
/// 用法：@setFlag flag:act1_spring_gaze
/// </summary>
[CommandAlias("setFlag")]
public class SetFlagCommand : Command
{
    [ParameterAlias("flag")]
    public StringParameter FlagName;

    public override UniTask Execute(AsyncToken token = default)
    {
        if (!string.IsNullOrEmpty(FlagName))
            StoryCardUnlockState.SetAdventureFlag(FlagName);
        return UniTask.CompletedTask;
    }
}
