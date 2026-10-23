/* Circuit Graph Analyzer - front end (plain JavaScript, no libraries).
   All mathematics is done by the .NET back end (POST /Home/Analyze, HomeController). */
(function () {
  'use strict';

  var $ = function (id) { return document.getElementById(id); };
  var state = { mode: 'circuit', data: null, method: 'loop', step: 0 };
  // The URL of the controller action comes from the Razor view (Url.Action), the examples from the controller's view model.
  var ANALYZE_URL = ($('app') && $('app').getAttribute('data-analyze-url')) || '/Home/Analyze';
  var EXAMPLES = {};
  try {
    JSON.parse($('examples-data').textContent).forEach(function (ex) { EXAMPLES[ex.key] = ex; });
  } catch (e) { EXAMPLES = {}; }

  var MATRIX_TEXT = {
    incidence: {
      label: 'Incidence matrix A',
      hint: 'Rows = nodes, columns = branches. +1 = the branch current leaves the node, −1 = it enters the node, 0 = not connected. ' +
            'A reduced matrix (reference row deleted) is also accepted.'
    },
    tieset: {
      label: 'Tie-set matrix B',
      hint: 'Rows = loops (one per link), columns = branches. The link columns must form a unit matrix. ' +
            'The number of nodes is then inferred (twigs + 1). The tree / reference options are ignored.'
    },
    cutset: {
      label: 'Cut-set matrix C',
      hint: 'Rows = cut-sets (one per twig), columns = branches. The twig columns must form a unit matrix. ' +
            'The number of nodes is then inferred (twigs + 1). The tree / reference options are ignored.'
    }
  };

  /* ------------------------------------------------------------------ helpers */
  function esc(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }
  function fmt(x, digits) {
    if (x === null || x === undefined || isNaN(x)) return '–';
    var d = digits === undefined ? 5 : digits;
    var v = Number(Number(x).toFixed(d));
    if (Object.is(v, -0)) v = 0;
    return String(v).replace('-', '−');
  }
  function defaultName(i) { return i < 26 ? String.fromCharCode(97 + i) : 'br' + (i + 1); }
  function isMatrixMode() { return state.mode !== 'circuit'; }

  /* ------------------------------------------------------------------ branch table */
  function addRow(v) {
    v = v || {};
    var tbody = $('branchTable').tBodies[0];
    var i = tbody.rows.length;
    var tr = document.createElement('tr');
    tr.innerHTML =
      '<td><input type="text" class="f-name" value="' + esc(v.name !== undefined ? v.name : defaultName(i)) + '" aria-label="Branch name"></td>' +
      '<td class="col-nodes"><input type="number" class="f-from" min="1" step="1" value="' + (v.from !== undefined ? v.from : '') + '" aria-label="From node"></td>' +
      '<td class="col-nodes"><input type="number" class="f-to" min="1" step="1" value="' + (v.to !== undefined ? v.to : '') + '" aria-label="To node"></td>' +
      '<td><input type="number" class="f-z" min="0" step="any" value="' + (v.z !== undefined ? v.z : 1) + '" aria-label="Impedance Z"></td>' +
      '<td><input type="number" class="f-e" step="any" value="' + (v.e !== undefined ? v.e : 0) + '" aria-label="Voltage source E"></td>' +
      '<td><input type="number" class="f-i" step="any" value="' + (v.i !== undefined ? v.i : 0) + '" aria-label="Current source I"></td>' +
      '<td><button type="button" class="del" title="Remove branch" aria-label="Remove branch">×</button></td>';
    tr.querySelector('.del').addEventListener('click', function () {
      if (tbody.rows.length > 1) tr.remove();
    });
    tbody.appendChild(tr);
  }

  function setRows(list) {
    var tbody = $('branchTable').tBodies[0];
    tbody.innerHTML = '';
    list.forEach(addRow);
  }

  function readRows() {
    var out = [];
    Array.prototype.forEach.call($('branchTable').tBodies[0].rows, function (tr) {
      var num = function (cls, dflt) {
        var raw = tr.querySelector(cls).value.trim();
        if (raw === '') return dflt;
        var n = Number(raw);
        return isNaN(n) ? dflt : n;
      };
      out.push({
        name: tr.querySelector('.f-name').value.trim(),
        from: num('.f-from', 0),
        to: num('.f-to', 0),
        z: num('.f-z', 0),
        e: num('.f-e', 0),
        i: num('.f-i', 0)
      });
    });
    return out;
  }

  /* ------------------------------------------------------------------ matrix text */
  function parseMatrix(text) {
    var rows = [];
    var lines = text.replace(/−/g, '-').replace(/[\[\]\(\)]/g, ' ').split(/\r?\n|;/);
    for (var l = 0; l < lines.length; l++) {
      var line = lines[l].trim();
      if (!line) continue;
      var parts = line.split(/[\s,]+/).filter(function (p) { return p !== ''; });
      var nums = [];
      for (var k = 0; k < parts.length; k++) {
        var n = Number(parts[k]);
        if (isNaN(n)) throw new Error('"' + parts[k] + '" is not a number (row ' + (rows.length + 1) + ').');
        nums.push(n);
      }
      rows.push(nums);
    }
    return rows;
  }

  function syncRowsToMatrix() {
    if (!isMatrixMode()) return;
    var cols = 0;
    try {
      var m = parseMatrix($('matrixText').value);
      cols = m.length ? m[0].length : 0;
    } catch (e) { return; }
    if (cols < 1 || cols > 60) return;
    var tbody = $('branchTable').tBodies[0];
    while (tbody.rows.length > cols) tbody.deleteRow(tbody.rows.length - 1);
    while (tbody.rows.length < cols) addRow();
  }

  /* ------------------------------------------------------------------ mode */
  function setMode(mode) {
    state.mode = mode;
    Array.prototype.forEach.call(document.querySelectorAll('#modeTabs .tab'), function (t) {
      var on = t.getAttribute('data-mode') === mode;
      t.classList.toggle('active', on);
      t.setAttribute('aria-selected', on ? 'true' : 'false');
    });
    var mm = isMatrixMode();
    $('matrixBox').hidden = !mm;
    $('branchBox').classList.toggle('matrix-mode', mm);
    $('btnAddBranch').hidden = mm;
    Array.prototype.forEach.call(document.querySelectorAll('#branchTable .del'), function (b) { b.hidden = mm; });
    $('branchHint').hidden = false;
    if (mm) {
      $('matrixLabel').textContent = MATRIX_TEXT[mode].label;
      $('matrixHint').textContent = MATRIX_TEXT[mode].hint;
      syncRowsToMatrix();
      Array.prototype.forEach.call(document.querySelectorAll('#branchTable .del'), function (b) { b.hidden = true; });
    }
  }

  /* ------------------------------------------------------------------ request */
  function buildRequest() {
    var req = { mode: state.mode, branches: readRows() };
    var tree = $('treeInput').value.split(/[\s,;]+/).filter(function (s) { return s; });
    if (tree.length) req.tree = tree;
    var ref = parseInt($('refInput').value, 10);
    if (!isNaN(ref)) req.reference = ref;
    if (isMatrixMode()) req.matrix = parseMatrix($('matrixText').value);
    return req;
  }

  function showError(msg) {
    var box = $('errorBox');
    box.textContent = msg;
    box.hidden = !msg;
  }

  function analyze() {
    showError('');
    var req;
    try { req = buildRequest(); } catch (e) { showError(e.message); return; }
    $('btnAnalyze').disabled = true;
    $('btnAnalyze').textContent = 'Analyzing…';
    fetch(ANALYZE_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req)
    }).then(function (r) {
      if (!r.ok) throw new Error('The server answered with status ' + r.status + '.');
      return r.json();
    }).then(function (data) {
      if (data.error) {
        showError(data.error);
        return;
      }
      state.data = data;
      state.method = data.loop && data.loop.ok ? 'loop' : 'cutset';
      state.step = 0;
      render();
      $('results').hidden = false;
      $('summaryCard').scrollIntoView({ behavior: 'smooth', block: 'start' });
    }).catch(function (err) {
      showError('Could not reach the .NET server (' + err.message + '). Start the project with "dotnet run" and open http://localhost:5080 .');
    }).then(function () {
      $('btnAnalyze').disabled = false;
      $('btnAnalyze').textContent = 'Analyze circuit';
    });
  }

  /* ------------------------------------------------------------------ rendering */
  function render() {
    var d = state.data;
    renderStats(d);
    renderGraph(d);
    renderTreeInfo(d);
    renderMatrices(d);
    renderSolution(d);
    renderSteps(d);
  }

  function stat(num, lbl, eq) {
    return '<div class="stat"><div class="num">' + esc(num) + '</div><div class="lbl">' + lbl + '</div>' +
           (eq ? '<div class="eq">' + eq + '</div>' : '') + '</div>';
  }

  function renderStats(d) {
    var h = '';
    h += stat(d.nodes, 'Nodes (N)', '');
    h += stat(d.branches, 'Branches (B)', '');
    h += stat(d.twigs, 'Tree branches', 'N − 1 = ' + (d.nodes - 1));
    h += stat(d.links, 'Links (L)', 'B − N + 1 = ' + d.branches + ' − ' + d.nodes + ' + 1');
    if (d.hasTopology && d.treeCount !== null && d.treeCount !== undefined) {
      h += stat(d.treeCount, 'Number of trees', 'det(A<sub>r</sub>A<sub>r</sub><sup>T</sup>)');
    } else {
      h += stat('n/a', 'Number of trees', 'needs the incidence matrix');
    }
    $('stats').innerHTML = h;
    var w = '';
    (d.warnings || []).forEach(function (t) { w += '<div class="note">' + esc(t) + '</div>'; });
    $('warnings').innerHTML = w;
  }

  /* --- graph drawing --- */
  function renderGraph(d) {
    var svg = $('graph');
    svg.classList.remove('dim');
    if (!d.hasTopology) {
      svg.innerHTML = '<text x="280" y="190" text-anchor="middle" fill="currentColor" font-size="15">' +
        'The graph cannot be drawn from a tie-set or cut-set matrix alone.</text>';
      return;
    }
    var n = d.nodes, cx = 280, cy = 185, R = n <= 2 ? 120 : 135;
    var ref = d.referenceNode;
    var pos = [];
    for (var i = 1; i <= n; i++) {
      var k = ((i - ref) % n + n) % n;               // reference node is placed at the bottom
      var ang = (Math.PI / 2) + 2 * Math.PI * k / n;
      pos[i] = { x: cx + R * Math.cos(ang), y: cy + R * Math.sin(ang) };
    }
    if (n === 2) { pos[1] = { x: cx - 120, y: cy }; pos[2] = { x: cx + 120, y: cy }; }

    var groups = {};
    d.edges.forEach(function (e, idx) {
      var key = Math.min(e.from, e.to) + '-' + Math.max(e.from, e.to);
      (groups[key] = groups[key] || []).push(idx);
    });

    var out = '';
    d.edges.forEach(function (e, idx) {
      var key = Math.min(e.from, e.to) + '-' + Math.max(e.from, e.to);
      var g = groups[key], m = g.length, j = g.indexOf(idx);
      var p0 = pos[e.from], p2 = pos[e.to];
      var dx = p2.x - p0.x, dy = p2.y - p0.y, len = Math.sqrt(dx * dx + dy * dy) || 1;
      var nx = -dy / len, ny = dx / len;
      var off = (j - (m - 1) / 2) * 46;
      var c = { x: (p0.x + p2.x) / 2 + nx * off * 2, y: (p0.y + p2.y) / 2 + ny * off * 2 };
      var mid = { x: 0.25 * p0.x + 0.5 * c.x + 0.25 * p2.x, y: 0.25 * p0.y + 0.5 * c.y + 0.25 * p2.y };
      var ux = dx / len, uy = dy / len;
      var cls = e.isTree ? 'tree' : 'link';
      var ax = mid.x, ay = mid.y;
      var arrow = (ax + ux * 8) + ',' + (ay + uy * 8) + ' ' +
                  (ax - ux * 6 + uy * 6) + ',' + (ay - uy * 6 - ux * 6) + ' ' +
                  (ax - ux * 6 - uy * 6) + ',' + (ay - uy * 6 + ux * 6);
      var side = off === 0 ? 1 : (off > 0 ? 1 : -1);
      var lx = mid.x + nx * side * 16, ly = mid.y + ny * side * 16;
      out += '<g class="edge-group" data-name="' + esc(e.name) + '">' +
        '<path class="edge ' + cls + '" d="M' + p0.x + ',' + p0.y + ' Q' + c.x + ',' + c.y + ' ' + p2.x + ',' + p2.y + '"/>' +
        '<polygon class="arrow ' + cls + '" points="' + arrow + '"/>' +
        '<text class="elabel" x="' + lx + '" y="' + ly + '" text-anchor="middle" dominant-baseline="central">' + esc(e.name) + '</text>' +
        '</g>';
    });
    for (var q = 1; q <= n; q++) {
      out += '<g class="node' + (q === ref ? ' ref' : '') + '"><circle cx="' + pos[q].x + '" cy="' + pos[q].y + '" r="19"/>' +
             '<text x="' + pos[q].x + '" y="' + pos[q].y + '">' + q + '</text></g>';
    }
    svg.innerHTML = out;
  }

  function highlightEdge(name) {
    var svg = $('graph');
    var groups = svg.querySelectorAll('.edge-group');
    if (!name) { svg.classList.remove('dim'); }
    else { svg.classList.add('dim'); }
    Array.prototype.forEach.call(groups, function (g) {
      g.classList.toggle('hot', !!name && g.getAttribute('data-name') === name);
    });
  }

  function renderTreeInfo(d) {
    var h = '<h3>Tree chosen for B and C</h3><div class="chipline">';
    d.treeBranches.forEach(function (n) { h += '<span class="pill">' + esc(n) + '</span>'; });
    h += '</div><h3>Links (co-tree)</h3><div class="chipline">';
    if (d.linkBranches.length === 0) h += '<span class="muted">none</span>';
    d.linkBranches.forEach(function (n) { h += '<span class="pill link">' + esc(n) + '</span>'; });
    h += '</div>';
    if (d.hasTopology) {
      h += '<h3>All trees of the graph (' + esc(d.treeCount) + ')</h3>';
      if (d.treesSkipped) {
        h += '<p class="muted">The graph is too large to list every tree; only the count is shown.</p>';
      } else {
        h += '<div class="chipline">';
        d.trees.forEach(function (t) { h += '<span class="pill link">' + esc(t.replace(/,/g, ' ')) + '</span>'; });
        h += '</div>';
        if (d.treesTruncated) h += '<p class="muted">Showing the first ' + d.trees.length + ' trees.</p>';
      }
    }
    $('treeInfo').innerHTML = h;
  }

  /* --- matrices --- */
  function matrixHTML(m, splitAt) {
    var h = '<div class="mscroll"><table class="m"><thead><tr><th></th>';
    m.colLabels.forEach(function (c, j) {
      h += '<th' + (splitAt && j === splitAt ? ' class="split"' : '') + '>' + esc(c) + '</th>';
    });
    h += '</tr></thead><tbody>';
    m.data.forEach(function (row, i) {
      h += '<tr><th class="rl">' + esc(m.rowLabels[i]) + '</th>';
      row.forEach(function (v, j) {
        var cls = Math.abs(v) < 1e-9 ? 'z' : (v > 0 ? 'p' : 'n');
        var txt = Math.abs(v) < 1e-9 ? '0' : (Math.abs(v - Math.round(v)) < 1e-9 ? (v > 0 ? '+' : '−') + Math.abs(Math.round(v)) : fmt(v));
        if (cls !== 'z' && Math.abs(Math.abs(v) - 1) > 1e-9) txt = fmt(v);
        h += '<td class="' + cls + (splitAt && j === splitAt ? ' split' : '') + '">' + txt + '</td>';
      });
      h += '</tr>';
    });
    return h + '</tbody></table></div>';
  }

  function matrixBox(m, caption, splitAt) {
    return '<div class="mbox"><h3>' + esc(m.label) + '</h3>' + matrixHTML(m, splitAt) +
           (caption ? '<div class="mcap">' + caption + '</div>' : '') + '</div>';
  }

  function renderMatrices(d) {
    var h = '<div class="mgrid">';
    if (d.a) h += matrixBox(d.a, '+1 leaves the node &middot; −1 enters the node. Every column sums to 0.');
    if (d.reduced) h += matrixBox(d.reduced, 'Reference row deleted: N − 1 = ' + (d.nodes - 1) + ' independent KCL equations.');
    h += matrixBox(d.b, 'Columns ordered [twigs | links]. The blue line separates the unit matrix part.', d.twigs);
    h += matrixBox(d.c, 'Columns ordered [twigs | links]. Twig columns form the unit matrix U.', d.twigs);
    h += '</div>';
    $('matrices').innerHTML = h;
  }

  /* --- solution --- */
  function vecLine(label, names, vals) {
    var parts = [];
    for (var i = 0; i < names.length; i++) parts.push(fmt(vals[i], 4));
    return '<div class="vec"><b>' + label + '</b> = [ ' + parts.join(' , ') + ' ]<sup>T</sup> &nbsp;<span class="muted">(' + names.map(esc).join(', ') + ')</span></div>';
  }

  function renderSolution(d) {
    var loopOk = d.loop && d.loop.ok, cutOk = d.cutSet && d.cutSet.ok;
    var h = '';
    if (!loopOk && !cutOk) {
      h += '<div class="error">' + esc((d.loop && d.loop.error) || (d.cutSet && d.cutSet.error) || 'No solution.') + '</div>';
      $('solution').innerHTML = h;
      return;
    }

    h += '<div class="actions" style="margin:0 0 14px">';
    h += '<span class="muted">Method:</span>';
    h += '<button type="button" class="btn small' + (state.method === 'loop' ? ' primary' : '') + '" data-method="loop"' + (loopOk ? '' : ' disabled') + '>Loop (tie-set)</button>';
    h += '<button type="button" class="btn small' + (state.method === 'cutset' ? ' primary' : '') + '" data-method="cutset"' + (cutOk ? '' : ' disabled') + '>Cut-set</button>';
    h += '</div>';
    if (!loopOk && d.loop) h += '<div class="note">Loop method unavailable: ' + esc(d.loop.error) + '</div>';
    if (!cutOk && d.cutSet) h += '<div class="note">Cut-set method unavailable: ' + esc(d.cutSet.error) + '</div>';

    var r = state.method === 'loop' ? d.loop : d.cutSet;
    var names = d.results.map(function (x) { return x.name; });
    h += '<div class="answer">' + vecLine('J<sub>B</sub>', names, r.j) + vecLine('V<sub>B</sub>', names, r.v) + '</div>';

    h += '<div class="tablewrap"><table class="res"><thead><tr><th>Branch</th><th>Nodes</th><th>Type</th><th>Z (Ω)</th><th>E (V)</th><th>I (A)</th><th>J<sub>B</sub> (A)</th><th>V<sub>B</sub> (V)</th></tr></thead><tbody>';
    d.results.forEach(function (x, i) {
      var nodes = d.hasTopology ? x.from + ' → ' + x.to : '–';
      h += '<tr data-name="' + esc(x.name) + '"><td><b>' + esc(x.name) + '</b></td><td>' + nodes + '</td>' +
           '<td><span class="tag' + (x.isTree ? '' : ' l') + '">' + (x.isTree ? 'twig' : 'link') + '</span></td>' +
           '<td class="num">' + fmt(x.z) + '</td><td class="num">' + fmt(x.e) + '</td><td class="num">' + fmt(x.i) + '</td>' +
           '<td class="num"><b>' + fmt(r.j[i]) + '</b></td><td class="num"><b>' + fmt(r.v[i]) + '</b></td></tr>';
    });
    h += '</tbody></table></div>';

    if (r.unknowns && r.unknowns.length) {
      if (state.method === 'loop') {
        h += '<h3>Loop currents I<sub>L</sub></h3>' + vecLine('I<sub>L</sub>', d.linkBranches, r.unknowns);
      } else {
        h += '<h3>Node-pair (twig) voltages V<sub>T</sub></h3>' + vecLine('V<sub>T</sub>', d.treeBranches, r.unknowns);
      }
    }
    if (r.note) h += '<p class="muted">' + esc(r.note) + '</p>';

    h += '<h3>Automatic verification</h3><ul class="checks">';
    d.checks.forEach(function (c) {
      h += '<li><span class="' + (c.passed ? 'ok' : 'bad') + '">' + (c.passed ? '✓' : '✗') + '</span> ' + esc(c.label) + ' = ' + c.value.toExponential(2) + '</li>';
    });
    h += '</ul>';
    h += '<p class="muted">Sign convention: V<sub>B</sub> = Z (J<sub>B</sub> + I<sub>s</sub>) − E<sub>s</sub>, so the branch voltage of a source branch is negative when the source delivers power.</p>';
    $('solution').innerHTML = h;
  }

  /* --- steps --- */
  function stepHTML(s, withMatrices) {
    var h = '<h3>' + esc(s.title) + '</h3><p class="txt">' + esc(s.text) + '</p>';
    if (withMatrices) {
      h += '<div class="mgrid">';
      (s.matrices || []).forEach(function (m) { h += '<div class="mbox"><h3>' + esc(m.label) + '</h3>' + matrixHTML(m) + '</div>'; });
      h += '</div>';
    }
    return h;
  }

  function renderSteps(d) {
    var steps = d.steps || [];
    var chips = '';
    steps.forEach(function (s, i) {
      chips += '<button type="button" class="chip' + (i === state.step ? ' active' : (i < state.step ? ' done' : '')) + '" data-step="' + i + '">' + (i + 1) + '</button>';
    });
    $('stepChips').innerHTML = chips;
    var s = steps[state.step];
    $('stepBody').innerHTML = s ? stepHTML(s, true) : '';
    $('stepBody').style.animation = 'none';
    void $('stepBody').offsetWidth;
    $('stepBody').style.animation = '';
    $('btnPrev').disabled = state.step === 0;
    $('btnNext').disabled = state.step >= steps.length - 1;

    var p = '<h2>Step-by-step solution</h2>';
    steps.forEach(function (st, i) { p += '<div style="margin-bottom:14px">' + stepHTML(st, true).replace('<h3>', '<h3>' + (i + 1) + '. ') + '</div>'; });
    $('stepsPrint').innerHTML = p;
  }

  /* ------------------------------------------------------------------ examples (served by HomeController) */
  function loadPreset(key) {
    var p = EXAMPLES[key];
    if (!p) return;
    showError('');
    $('treeInput').value = p.tree || '';
    $('refInput').value = p.reference || '';
    if (p.mode === 'circuit') {
      setMode('circuit');
      setRows(p.branches);
    } else {
      $('matrixText').value = p.matrix;
      setMode(p.mode);
      setRows(p.names.map(function (n) { return { name: n, z: 1, e: 0, i: 0 }; }));
      setMode(p.mode);
    }
    analyze();
  }

  /* ------------------------------------------------------------------ image */
  function showImage(file) {
    if (!file || !/^image\//.test(file.type)) { showError('Please choose an image file (png, jpg, …).'); return; }
    var reader = new FileReader();
    reader.onload = function () {
      $('imgPreview').src = reader.result;
      $('imgPreview').hidden = false;
      $('dropText').hidden = true;
      $('btnClearImg').hidden = false;
    };
    reader.readAsDataURL(file);
  }

  /* ------------------------------------------------------------------ wiring */
  function init() {
    var first = EXAMPLES.lec3;
    setRows(first ? first.branches : [{ name: 'a', from: 1, to: 2, z: 1, e: 0, i: 0 }]);
    if (first) { $('refInput').value = first.reference || ''; }
    setMode('circuit');

    Array.prototype.forEach.call(document.querySelectorAll('#modeTabs .tab'), function (t) {
      t.addEventListener('click', function () {
        var mode = t.getAttribute('data-mode');
        if (mode !== 'circuit' && !$('matrixText').value.trim()) {
          $('matrixText').value = '';
        }
        setMode(mode);
        showError('');
      });
    });

    $('btnAddBranch').addEventListener('click', function () { addRow(); });
    $('btnAnalyze').addEventListener('click', analyze);
    $('matrixText').addEventListener('input', syncRowsToMatrix);
    $('presetSelect').addEventListener('change', function () {
      if (this.value) loadPreset(this.value);
      this.value = '';
    });
    $('btnPrint').addEventListener('click', function () { window.print(); });

    $('imgFile').addEventListener('change', function () { showImage(this.files[0]); });
    $('btnClearImg').addEventListener('click', function () {
      $('imgPreview').hidden = true; $('imgPreview').removeAttribute('src');
      $('dropText').hidden = false; $('btnClearImg').hidden = true; $('imgFile').value = '';
    });
    var dz = $('dropZone');
    ['dragenter', 'dragover'].forEach(function (ev) {
      dz.addEventListener(ev, function (e) { e.preventDefault(); dz.classList.add('over'); });
    });
    ['dragleave', 'drop'].forEach(function (ev) {
      dz.addEventListener(ev, function (e) { e.preventDefault(); dz.classList.remove('over'); });
    });
    dz.addEventListener('drop', function (e) { if (e.dataTransfer.files.length) showImage(e.dataTransfer.files[0]); });

    // results interactions (event delegation)
    $('solution').addEventListener('click', function (e) {
      var b = e.target.closest('[data-method]');
      if (b && !b.disabled) { state.method = b.getAttribute('data-method'); renderSolution(state.data); }
    });
    $('solution').addEventListener('mouseover', function (e) {
      var tr = e.target.closest('tr[data-name]');
      highlightEdge(tr ? tr.getAttribute('data-name') : null);
    });
    $('solution').addEventListener('mouseleave', function () { highlightEdge(null); });

    $('stepChips').addEventListener('click', function (e) {
      var c = e.target.closest('[data-step]');
      if (c) { state.step = Number(c.getAttribute('data-step')); renderSteps(state.data); }
    });
    $('btnPrev').addEventListener('click', function () { if (state.step > 0) { state.step--; renderSteps(state.data); } });
    $('btnNext').addEventListener('click', function () {
      if (state.data && state.step < state.data.steps.length - 1) { state.step++; renderSteps(state.data); }
    });
  }

  init();
})();
