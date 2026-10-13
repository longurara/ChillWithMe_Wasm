(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const root = $('chill-ui');
  const titles = {music:['GIAI ĐIỆU CHO HÔM NAY','Thư viện nhạc'],timer:['MỘT VIỆC MỖI LÚC','Tập trung'],notes:['GÓC CỦA BẠN','Ghi chú & công việc'],story:['Ở BÊN SATONE','Câu chuyện'],settings:['CHỌN MỘT KHÔNG KHÍ','Không gian'],save:['GIỮ LẠI HÀNH TRÌNH','Tiến trình']};
  let instance, receiver, state, quiet = false, noteTimer, searchTimer, pendingNote = null, musicQuery = '';
  const signatures = new Map();
  const command = (action, values={}) => {
    if (instance && receiver) instance.SendMessage(receiver,'UICommand',JSON.stringify({action,...values}));
  };
  const connect = () => { if (instance && receiver) instance.SendMessage(receiver,'ConnectUI',''); };
  const text = (id,value) => { const el=$(id); if (el.textContent !== String(value ?? '')) el.textContent=value ?? ''; };
  const input = (id,value) => { const el=$(id); if (document.activeElement !== el && el.value !== String(value ?? '')) el.value=value ?? ''; };
  const clock = seconds => `${Math.floor(Math.max(0,seconds)/60).toString().padStart(2,'0')}:${Math.floor(Math.max(0,seconds)%60).toString().padStart(2,'0')}`;
  const empty = (list,label) => { const p=document.createElement('p'); p.className='empty'; p.textContent=label; list.append(p); };
  const listUpdate = (id,items,draw,emptyLabel) => {
    const signature=JSON.stringify(items); if (signatures.get(id) === signature) return;
    signatures.set(id,signature); const list=$(id), scroll=list.scrollTop; list.replaceChildren();
    if (!items.length) { if (emptyLabel) empty(list,emptyLabel); }
    else items.forEach((item,index) => draw(list,item,index));
    list.scrollTop=scroll;
  };
  const row = (item,index,action) => {
    const button=document.createElement('button'); button.type='button'; button.className='list-row'; button.disabled=!item.enabled;
    const number=document.createElement('span'); number.className='row-number'; number.textContent=item.done ? '✓' : String(index+1).padStart(2,'0');
    const content=document.createElement('span'); content.className='row-text';
    const title=document.createElement('span'); title.className='row-title'; title.textContent=item.title; content.append(title);
    if (item.detail) { const detail=document.createElement('span'); detail.className='row-detail'; detail.textContent=item.detail; content.append(detail); }
    button.append(number,content); button.addEventListener('click',action); return button;
  };
  function flushNote() {
    clearTimeout(noteTimer);
    if (pendingNote !== null) { const value=pendingNote; pendingNote=null; command('note',{text:value}); text('note-state','Tự động lưu'); }
  }
  function openPanel(panel) {
    flushNote(); command('panel',{text:state?.panel === panel ? 'none' : panel});
  }
  function renderMusic() {
    if (!state || state.panel !== 'music') return;
    const tracks=(state.tracks || []).filter(t => t.title.toLocaleLowerCase().includes(musicQuery));
    text('music-count',`${tracks.length} bản nhạc`);
    text('music-loading',state.musicStatus || '');
    $('story-music-hint').hidden=!state.story.playing;
    listUpdate('track-list',tracks.map(t=>({...t,enabled:state.story.ready && !state.musicBusy && !state.story.playing,active:t.title === state.musicTitle})),(list,item,index) => {
      const button=row(item,index,()=>command('music-select',{id:item.id})); button.classList.toggle('active',item.active); if (item.active) button.setAttribute('aria-current','true'); list.append(button);
    }, musicQuery ? 'Chưa tìm thấy bài hát phù hợp.' : 'Đang chuẩn bị thư viện nhạc…');
  }
  function render(next) {
    state=next; const s=state.story;
    root.hidden=false; root.classList.toggle('story-mode',s.playing); root.classList.toggle('quiet-mode',quiet);
    root.classList.toggle('panel-open',state.panel !== 'none');
    $('show-ui').hidden=!quiet;
    // Only enable actions after the save and story tables have finished loading.
    root.querySelectorAll('button,input,textarea,select').forEach(el=>{if (!el.closest('.list-row,.choices,.todo-row')) el.disabled=!s.ready;});
    $('fullscreen').disabled=$('hide-ui').disabled=$('show-ui').disabled=false;
    text('level-chip',`Cấp ${s.level}`);
    text('timer-chip-label',`${s.timerText} · ${s.breakTime ? 'Nghỉ' : 'Tập trung'}`);
    $('timer-chip').classList.toggle('running',s.timerRunning);
    root.querySelectorAll('[data-mode]').forEach(el=>{el.disabled=!s.ready || s.playing; el.setAttribute('aria-pressed',Number(el.dataset.mode) === state.mode);});
    root.querySelectorAll('[data-time]').forEach(el=>{el.disabled=!s.ready || s.playing; el.setAttribute('aria-pressed',Number(el.dataset.time) === state.timeMode);});
    root.querySelectorAll('[data-panel]').forEach(el=>el.setAttribute('aria-pressed',el.dataset.panel === state.panel));
    $('panel').hidden=state.panel === 'none';
    root.querySelectorAll('[data-view]').forEach(el=>el.hidden=el.dataset.view !== state.panel);
    if (titles[state.panel]) {text('panel-eyebrow',titles[state.panel][0]); text('panel-title',titles[state.panel][1]);}
    text('panel-status',s.status);
    input('volume',state.volume); text('volume-label',`${Math.round(state.volume*100)}%`);
    $('music-play-icon').setAttribute('href',state.musicPlaying ? '#i-pause' : '#i-play');
    $('music-toggle').setAttribute('aria-label',state.musicPlaying ? 'Tạm dừng nhạc' : 'Phát nhạc');
    $('music-toggle').disabled=state.musicBusy || !s.ready;
    text('music-title',state.musicBusy ? 'Đang tải nhạc…' : state.musicTitle);
    text('music-position',clock(state.musicTime)); renderMusic();
    if (state.panel === 'timer') {
      text('timer-clock',s.timerText); text('timer-phase',s.breakTime ? 'Thả lỏng một chút nhé.' : s.timerRunning ? 'Cùng Satone làm việc thôi.' : 'Đến giờ tập trung.');
      const rounds=Array.from({length:4},(_,i)=>i < s.rounds%4 || s.rounds > 0 && s.rounds%4 === 0);
      listUpdate('timer-rounds',rounds,(list,done)=>{const dot=document.createElement('span');dot.className=done?'done':'';list.append(dot);},'');
      input('work-minutes',s.workMinutes); input('break-minutes',s.breakMinutes);
      $('work-minutes').disabled=$('break-minutes').disabled=s.timerRunning || !s.ready;
      text('timer-toggle',s.timerRunning ? 'Tạm dừng' : 'Bắt đầu / tiếp tục');
      text('work-total',`Đã tập trung ${s.workedText}`); text('exp-label',`${Math.floor(s.exp)} / ${Math.floor(s.requiredExp)} EXP`);
      $('exp-progress').max=Math.max(1,s.requiredExp);$('exp-progress').value=s.exp;
    }
    if (state.panel === 'notes') {
      if (pendingNote === null) input('note',s.note || '');
      text('todo-count',`${(s.todos||[]).filter(t=>t.done).length}/${(s.todos||[]).length}`);
      listUpdate('todo-list',s.todos||[],(list,todo,index)=>{
        const div=document.createElement('div');div.className=`todo-row${todo.done?' done':''}`;
        const check=document.createElement('input');check.type='checkbox';check.checked=todo.done;check.setAttribute('aria-label',`Hoàn thành ${todo.title}`);check.addEventListener('change',()=>command('todo-toggle',{value:index}));
        const label=document.createElement('span');label.textContent=todo.title;
        const remove=document.createElement('button');remove.type='button';remove.textContent='×';remove.setAttribute('aria-label',`Xóa ${todo.title}`);remove.addEventListener('click',()=>command('todo-delete',{value:index}));
        div.append(check,label,remove);list.append(div);
      },'Một việc nhỏ cũng là một bước tiến.');
    }
    if (state.panel === 'story') {
      $('story-resume').hidden=!s.chapter || s.playing;
      input('chapter-kind',s.kind);input('language',s.language);$('unlock').checked=s.unlock;
      listUpdate('chapter-list',s.chapters||[],(list,item,index)=>list.append(row(item,index,()=>{flushNote();command('story-start',{id:item.id});command('panel',{text:'none'});})),s.playing ? 'Bạn đang đọc truyện. Nhật ký ở phía dưới.' : 'Chưa tìm thấy câu chuyện phù hợp.');
      text('chapter-page',`${s.chapterCount} đoạn · ${s.page+1}/${Math.max(1,Math.ceil(s.chapterCount/20))}`);
      $('chapter-prev').disabled=s.page <= 0 || s.playing; $('chapter-next').disabled=(s.page+1)*20 >= s.chapterCount || s.playing;
      listUpdate('dialogue-history',s.history||[],(list,line)=>{const p=document.createElement('p');p.textContent=line;list.append(p);},'Nhật ký sẽ hiện khi bạn đọc truyện.');
    }
    if (state.panel === 'settings') {
      listUpdate('environment',state.environments||[],(list,item)=>{const option=document.createElement('option');option.value=item.id;option.textContent=item.title;list.append(option);},'');
      input('environment',state.windowMode);$('environment').disabled=s.playing || !s.ready;$('quality').checked=state.lowQuality;
    }
    if (state.panel === 'save') {
      text('save-summary',`Cấp ${s.level} · ${s.completed} đoạn đã đọc\nĐã tập trung ${s.workedText}`);
      $('save-now').disabled=!s.saveAllowed || !s.ready;
    }
    $('dialogue').hidden=!s.playing; text('speaker',s.speaker || 'CÂU CHUYỆN');text('dialogue-title',s.title);text('dialogue-text',s.text);
    $('voice-loading').hidden=!s.loadingAudio; $('auto-read').checked=s.autoRead;
    listUpdate('choices',s.choices||[],(list,item,index)=>{const button=row(item,index,()=>command('choice',{value:index}));button.className='quiet';list.append(button);},'');
    $('dialogue-next').hidden=!!s.choices?.length; text('dialogue-next',s.typing ? 'Hiện hết chữ' : 'Tiếp →');
  }
  window.chillUI={bind(value){receiver=value;connect();},ready(value){instance=value;connect();},update:render};
  root.querySelectorAll('[data-panel]').forEach(el=>el.addEventListener('click',()=>openPanel(el.dataset.panel)));
  root.querySelectorAll('[data-mode]').forEach(el=>el.addEventListener('click',()=>command('mode',{value:Number(el.dataset.mode)})));
  root.querySelectorAll('[data-time]').forEach(el=>el.addEventListener('click',()=>command('time',{value:Number(el.dataset.time)})));
  $('close-panel').addEventListener('click',()=>{flushNote();command('panel',{text:'none'});});
  $('hide-ui').addEventListener('click',()=>{flushNote();quiet=true;root.classList.add('quiet-mode');$('show-ui').hidden=false;});
  $('show-ui').addEventListener('click',()=>{quiet=false;root.classList.remove('quiet-mode');$('show-ui').hidden=true;});
  $('fullscreen').hidden=!document.documentElement.requestFullscreen;
  $('fullscreen').addEventListener('click',async()=>{try{if(document.fullscreenElement)await document.exitFullscreen();else if(document.documentElement.requestFullscreen)await document.documentElement.requestFullscreen();}catch(_){text('panel-status','Trình duyệt chưa cho phép toàn màn hình.');}});
  $('music-toggle').addEventListener('click',()=>command('music-toggle'));
  $('music-search').addEventListener('input',event=>{musicQuery=event.target.value.toLocaleLowerCase().trim();renderMusic();});
  $('volume').addEventListener('input',event=>command('volume',{value:Number(event.target.value)}));
  $('quality').addEventListener('change',event=>command('quality',{value:event.target.checked?1:0}));
  $('environment').addEventListener('change',event=>command('environment',{value:Number(event.target.value)}));
  ['work-minutes','break-minutes'].forEach(id=>$(id).addEventListener('change',event=>command(id,{value:Number(event.target.value)})));
  $('timer-toggle').addEventListener('click',()=>command('timer-toggle'));$('timer-reset').addEventListener('click',()=>command('timer-reset'));
  $('note').addEventListener('input',event=>{pendingNote=event.target.value;text('note-state','Đang ghi…');clearTimeout(noteTimer);noteTimer=setTimeout(flushNote,400);});$('note').addEventListener('blur',flushNote);
  $('todo-form').addEventListener('submit',event=>{event.preventDefault();const title=$('todo-input').value.trim();if(title){command('todo-add',{text:title});$('todo-input').value='';}});
  $('chapter-search').addEventListener('input',event=>{clearTimeout(searchTimer);const value=event.target.value;searchTimer=setTimeout(()=>command('chapter-search',{text:value}),250);});
  $('chapter-kind').addEventListener('change',event=>command('chapter-kind',{value:Number(event.target.value)}));
  $('language').addEventListener('change',event=>command('language',{text:event.target.value}));$('unlock').addEventListener('change',event=>command('unlock',{value:event.target.checked?1:0}));
  $('chapter-prev').addEventListener('click',()=>command('chapter-page',{value:state.story.page-1}));$('chapter-next').addEventListener('click',()=>command('chapter-page',{value:state.story.page+1}));
  $('story-resume').addEventListener('click',()=>{command('story-resume');command('panel',{text:'none'});});
  ['save-now','save-export','save-import'].forEach(id=>$(id).addEventListener('click',()=>{flushNote();command(id==='save-now'?'save':id);}));
  $('story-stop').addEventListener('click',()=>command('story-stop'));$('dialogue-next').addEventListener('click',()=>command('next'));$('auto-read').addEventListener('change',event=>command('auto',{value:event.target.checked?1:0}));
  document.addEventListener('keydown',event=>{if(event.key==='Escape'){if(quiet){quiet=false;root.classList.remove('quiet-mode');$('show-ui').hidden=true;}else{flushNote();command('panel',{text:'none'});}}});
  window.addEventListener('pagehide',flushNote);
})();
