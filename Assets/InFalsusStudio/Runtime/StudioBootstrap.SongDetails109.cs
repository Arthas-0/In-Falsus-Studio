using System;
using System.Globalization;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private static readonly string[] songDetailsFields={"field_song_preview_start","field_song_preview_end","field_song_jacket_designer"};
        private bool SongDetailsInputsPending()
        {foreach(string name in songDetailsFields){FieldDraft field;if(fieldDrafts.TryGetValue(name,out field)&&field.Dirty)return true;}return false;}
        private void CommitSongDetailsInputsNow()
        {foreach(string name in songDetailsFields)if(!CommitFieldNow(name))throw new ArgumentException("Song preview/jacket credit input is invalid; correct it before continuing.");}
        private void CheckSongDetailsOwner(SongFolderProject expected,int index)
        {if(expected==null||expected!=songProject||index!=expected.ActiveDifficulty)throw new InvalidOperationException("Song information draft belongs to another song/difficulty.");}
        private void SetPreviewTime(SongFolderProject expected,int index,bool start,string text)
        {
            CheckSongDetailsOwner(expected,index);
            double seconds;if(!double.TryParse(text,NumberStyles.Float,CI,out seconds))throw new ArgumentException(L("请输入以秒为单位的数字。","Enter a number of seconds."));
            NativeSongDetails.StagePreviewTime(expected,start,seconds);RefreshProjectDirty();
            status=L("预览时间已更新到草稿；结束须晚于开始，Ctrl+S 保存。","Preview time updated in RAM. End must follow start; Ctrl+S saves.");
        }
        private void SetJacketDesigner(SongFolderProject expected,int index,string value)
        {
            CheckSongDetailsOwner(expected,index);NativeSongDetails.StageJacketDesigner(expected,index,value);RefreshProjectDirty();
            status=L("当前难度的曲绘画师已更新到草稿；Ctrl+S 保存。","Jacket Designer updated in RAM for this difficulty; Ctrl+S saves.");
        }
        private void DrawSongDetailsInputs(SongFolderProject expected,int index)
        {
            CommittedRow(L("预览开始（秒）","Preview start (seconds)"),songDetailsFields[0],expected.PreviewStartSeconds.ToString("R",CI),v=>SetPreviewTime(expected,index,true,v));
            CommittedRow(L("预览结束（秒）","Preview end (seconds)"),songDetailsFields[1],expected.PreviewEndSeconds.ToString("R",CI),v=>SetPreviewTime(expected,index,false,v));
            GUILayout.Label(L("起止均为秒，结束须晚于开始；0 / 0 表示未设置。","Both times use seconds. End must follow start; 0 / 0 is unset."));
            GUILayout.Label(L("曲绘画师（当前难度）","Jacket Designer (this difficulty)"));
            Rect rect=GUILayoutUtility.GetRect(30,Mathf.Max(22,25*hudScale),GUILayout.ExpandWidth(true));
            CommittedField(rect,songDetailsFields[2],expected.Difficulties[index].JacketDesigner,v=>SetJacketDesigner(expected,index,v),false,true);
        }
        private void RememberOpenedJacket(SongFolderProject project)
        {
            try
            {
                string key=SongMediaDiscovery.PreferenceKey(project.Root);
                PlayerPrefs.SetString(key+".file",project.Relative(project.JacketPath));PlayerPrefs.SetString(key+".inventory",project.JacketInventorySha256);PlayerPrefs.Save();
            }
            catch(Exception ex){Debug.LogWarning("Jacket selection preference: "+ex.Message,this);}
        }
    }
}
