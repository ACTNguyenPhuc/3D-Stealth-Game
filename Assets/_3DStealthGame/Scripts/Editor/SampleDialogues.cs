using UnityEditor;
using UnityEngine;
using static StealthGame.DialogueActionType;
using static StealthGame.DialogueConditionType;

namespace StealthGame.EditorTools
{
    /// <summary>
    /// Sinh nội dung hội thoại của game:
    ///  - Ivan: người giao nhiệm vụ, chỉ đường, cửa hàng đổi mảnh vụn lấy vật phẩm.
    ///  - Whisper: hồn ma thân thiện – trợ giúp nếu được Ivan giới thiệu, báo động nếu bị khiêu khích.
    /// </summary>
    public static class SampleDialogues
    {
        public static (DialogueTree ivan, DialogueTree whisper) CreateOrUpdate(string folder, bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                var parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
            }

            var ivan = GetOrCreate(folder + "/Dialogue_Ivan.asset", overwrite, out bool fillIvan);
            if (fillIvan) BuildIvan(ivan);
            var whisper = GetOrCreate(folder + "/Dialogue_Whisper.asset", overwrite, out bool fillWhisper);
            if (fillWhisper) BuildWhisper(whisper);

            AssetDatabase.SaveAssets();
            return (ivan, whisper);
        }

        static DialogueTree GetOrCreate(string path, bool overwrite, out bool needsFill)
        {
            var tree = AssetDatabase.LoadAssetAtPath<DialogueTree>(path);
            if (tree != null && !overwrite) { needsFill = false; return tree; }
            if (tree == null)
            {
                tree = ScriptableObject.CreateInstance<DialogueTree>();
                AssetDatabase.CreateAsset(tree, path);
            }
            tree.nodes.Clear();
            tree.entries.Clear();
            needsFill = true;
            EditorUtility.SetDirty(tree);
            return tree;
        }

        // ================================================================== Ivan

        static void BuildIvan(DialogueTree t)
        {
            t.npcName = "Ivan";
            t.startNodeId = "intro";
            t.entries.Add(Entry("welcome_back", new DialogueCondition(FlagSet, "ivan_met")));

            N(t, "intro", "Suỵt! Nhỏ tiếng thôi... Cậu cũng bị lời nguyền kéo vào dinh thự này à?")
                .Enter(new DialogueAction(SetFlag, "ivan_met"))
                .Choice("Ông là ai? Đây là đâu?", "who")
                .Choice("Làm sao để thoát ra?", "quest");

            N(t, "who", "Tôi là Ivan, người làm vườn. Từ khi lời nguyền giáng xuống, dinh thự chỉ tồn tại trong đêm. Bình minh tới, nó tan biến – cùng tất cả những ai còn kẹt bên trong.", next: "quest");

            N(t, "quest", "Cửa chính ở Cánh Đông bị phong ấn bằng ba Mảnh hồn. Tìm đủ 3 mảnh, cậu sẽ phá được phong ấn và thoát ra trước 6 giờ sáng.", next: "quest2");

            N(t, "quest2", "Một mảnh ở trong bếp, ngay gần đây. Mảnh thứ hai ở thư phòng phía Bắc, bị Phong ấn Đỏ bảo vệ. Mảnh cuối ở phòng ăn – nơi một Oan hồn canh giữ – bị Phong ấn Xanh khóa lại.", next: "menu")
                .Enter(new DialogueAction(SetFlag, "quest_given"));

            N(t, "welcome_back", "Lại là cậu à? Vẫn còn sống, tốt lắm.", next: "menu");

            var menu = N(t, "menu", "Cậu cần gì nữa?")
                .Choice("Tôi đã có đủ 3 Mảnh hồn!", "all_relics")
                    .If(RelicsAtLeast, "", 3).If(FlagNotSet, "ivan_blessing").Hidden();
            menu.Choice("Chìa khóa ở đâu?", "keys");
            menu.Choice("Làm sao đối phó với lũ ma?", "tips");
            menu.Choice("Đổi 6 mảnh vụn lấy 1 bình máu.", "buy_potion")
                .If(ShardsAtLeast, "", 6).Hint("cần 6 mảnh vụn")
                .Do(new DialogueAction(SpendShards, "", 6)).Do(new DialogueAction(GivePotions, "", 1));
            menu.Choice("Đổi 4 mảnh vụn lấy dầu đèn.", "buy_oil")
                .If(ShardsAtLeast, "", 4).Hint("cần 4 mảnh vụn")
                .Do(new DialogueAction(SpendShards, "", 4)).Do(new DialogueAction(RestoreEnergy, "", 60));
            menu.Choice("Ông biết gì về hồn ma Whisper?", "whisper_info");
            menu.Choice("Tạm biệt.", null);

            N(t, "keys", "Chìa khóa đỏ nằm trong căn phòng xanh ở Cánh Tây, nhưng hành lang có tượng Gargoyle canh. Đợi nó quay đi rồi ngồi xuống mà lẻn qua. Chìa khóa xanh ở căn phòng ô cờ nhỏ giữa nhà.", next: "menu");
            N(t, "tips", "Ma nhìn theo hình nón trước mặt. Ngồi xuống thì khó bị thấy, chạy thì chúng nghe tiếng. Nếu bị dồn, chiếu Linh Quang vào chúng – đánh từ phía sau càng đau. Ngọn nến xanh giúp cậu lưu lại tiến trình.", next: "menu");
            N(t, "buy_potion", "Đây, cầm lấy. Đừng chết vô ích.", next: "menu");
            N(t, "buy_oil", "Dầu đèn đây. Ánh sáng là thứ duy nhất lũ ma sợ.", next: "menu");
            N(t, "whisper_info", "Whisper là hồn ma hiền lành ở sảnh lớn. Nói với nó rằng Ivan gửi lời hỏi thăm – có thể nó sẽ giúp cậu.", next: "menu")
                .Enter(new DialogueAction(SetFlag, "ivan_trust"));
            N(t, "all_relics", "Tuyệt vời! Ta ban phước cho cậu. Mau tới cửa chính ở Cánh Đông – cầu cho cậu may mắn!", next: "thanks")
                .Enter(new DialogueAction(Heal, "", 100))
                .Enter(new DialogueAction(AddScore, "", 500))
                .Enter(new DialogueAction(SetFlag, "ivan_blessing"));
            N(t, "thanks", "Cảm ơn ông, Ivan. Tôi sẽ quay lại cứu ông.", speaker: "{player}");
        }

        // ================================================================== Whisper

        static void BuildWhisper(DialogueTree t)
        {
            t.npcName = "Whisper";
            t.startNodeId = "intro";
            t.entries.Add(Entry("after_help", new DialogueCondition(FlagSet, "whisper_helped")));

            var intro = N(t, "intro", "Ooooo... Một người sống... đi lạc vào đây sao...?")
                .Choice("Đừng sợ, tôi không làm hại ai.", "calm");
            intro.Choice("Ivan gửi lời hỏi thăm bạn.", "ivan_friend").If(FlagSet, "ivan_trust").Hint("Hãy hỏi Ivan về Whisper trước");
            intro.Choice("BÙ! Ma gì mà nhát thế!", "angry");
            intro.Choice("(Lặng lẽ rời đi)", null);

            N(t, "calm", "Ta không sợ... ta chỉ cô đơn. Những hồn ma khác không hiền như ta đâu.", next: "calm_menu");

            var menu = N(t, "calm_menu", "Ngươi muốn gì ở ta?");
            menu.Choice("Bạn có thể giúp tôi không?", "help_refuse").If(FlagNotSet, "ivan_trust").Hidden();
            menu.Choice("Ivan gửi lời hỏi thăm bạn.", "ivan_friend").If(FlagSet, "ivan_trust").Hint("Hãy hỏi Ivan về Whisper trước");
            menu.Choice("Mảnh hồn trong bếp có ai canh không?", "kitchen_hint").If(NodeNotVisited, "kitchen_hint").Hidden();
            menu.Choice("Tạm biệt.", null);

            N(t, "help_refuse", "Giúp ngươi? Ta còn chẳng biết ngươi là ai...", next: "calm_menu");
            N(t, "kitchen_hint", "Một hồn ma đi lại giữa bếp và phòng khách. Đợi nó quay lưng... rồi hãy lấy.", next: "calm_menu");
            N(t, "ivan_friend", "Ivan... người làm vườn tốt bụng... Được, vì ông ấy, ta sẽ giúp ngươi một lần.", next: "help_choice");

            N(t, "help_choice", "Ta có thể làm một trong ba việc. Chọn đi:")
                .Choice("Làm lũ ma chậm lại.", "slow")
                    .Do(new DialogueAction(SlowEnemies, "", 0.4f, 40f))
                    .Do(new DialogueAction(SetFlag, "whisper_helped"))
                .Back()
                .Choice("Mở cánh cửa đỏ giúp tôi.", "open")
                    .Do(new DialogueAction(OpenDoor, "keyRed"))
                    .Do(new DialogueAction(SetFlag, "whisper_helped"))
                .Back()
                .Choice("Cho tôi sức mạnh của ánh sáng.", "energy")
                    .Do(new DialogueAction(RestoreEnergy, "", 100))
                    .Do(new DialogueAction(GivePotions, "", 1))
                    .Do(new DialogueAction(SetFlag, "whisper_helped"));

            N(t, "slow", "Ta sẽ hát một khúc ru cho chúng... Đi nhanh lên, phép này không kéo dài lâu đâu!");
            N(t, "open", "Cánh cửa đỏ đã mở. Chúc ngươi may mắn, kẻ sống...");
            N(t, "energy", "Hãy nhận lấy ánh sáng của ta... và một chút sinh lực.");
            N(t, "angry", "NGƯƠI DÁM?! HỠI CÁC ANH EM, CÓ KẺ ĐỘT NHẬP!!!")
                .Enter(new DialogueAction(AlertEnemies));
            N(t, "after_help", "Ta đã giúp ngươi rồi. Đi đi, trước khi trời sáng...");
        }

        // ================================================================== Builder helpers

        static DialogueEntry Entry(string nodeId, params DialogueCondition[] conditions)
        {
            var e = new DialogueEntry { nodeId = nodeId };
            e.conditions.AddRange(conditions);
            return e;
        }

        static DialogueNode N(DialogueTree tree, string id, string text, string next = null, string speaker = "")
        {
            var node = new DialogueNode { id = id, text = text, nextNodeId = next ?? string.Empty, speaker = speaker };
            tree.nodes.Add(node);
            return node;
        }

        static DialogueNode Enter(this DialogueNode node, DialogueAction action)
        {
            node.onEnter.Add(action);
            return node;
        }

        static ChoiceBuilder Choice(this DialogueNode node, string text, string target)
        {
            var c = new DialogueChoice { text = text, targetNodeId = target ?? string.Empty };
            node.choices.Add(c);
            return new ChoiceBuilder(node, c);
        }

        class ChoiceBuilder
        {
            readonly DialogueNode m_Node;
            readonly DialogueChoice m_Choice;

            public ChoiceBuilder(DialogueNode node, DialogueChoice choice)
            {
                m_Node = node;
                m_Choice = choice;
            }

            public ChoiceBuilder If(DialogueConditionType type, string key, int amount = 0)
            {
                m_Choice.conditions.Add(new DialogueCondition(type, key, amount));
                return this;
            }

            public ChoiceBuilder Hidden() { m_Choice.showWhenLocked = false; return this; }
            public ChoiceBuilder Hint(string hint) { m_Choice.lockedHint = hint; return this; }
            public ChoiceBuilder Do(DialogueAction action) { m_Choice.actions.Add(action); return this; }
            public DialogueNode Back() => m_Node;
            public ChoiceBuilder Choice(string text, string target) => m_Node.Choice(text, target);
        }
    }
}
