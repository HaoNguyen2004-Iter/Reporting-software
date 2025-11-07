// ===== BIẾN TOÀN CỤC =====
var editorInstance = null;
var editorReadyResolve = null;
var editorReady = new Promise(function (resolve) { editorReadyResolve = resolve; });
var latestEditorHtml = ''; 

// ===== KHỞI TẠO KHI DOCUMENT READY =====
$(function () {
    // Disable "Tạo báo cáo" until editor is ready
    $('#createReport').prop('disabled', true);

    // Log CKEditor version (CKEditor 5 exposes CKEDITOR_VERSION)
    try {
        var ckVer = window.CKEDITOR_VERSION || (window.ClassicEditor && ClassicEditor.version) || 'unknown';
        console.log('CKEditor version:', ckVer);
    } catch { }

    // Khởi tạo CKEditor
    initCKEditor();

    // Event: Xuất PDF
    $('#createReport').on('click', handleExportPDF);

    console.log('Report.js đã được khởi tạo');
});

// ===== HÀM XỬ LÝ LOGIC =====

/**
 * Khởi tạo CKEditor
 */
function initCKEditor() {
    if (typeof ClassicEditor === 'undefined') {
        console.error('ClassicEditor chưa được load.');
        showNotification('Không tải được CKEditor. Vui lòng kiểm tra script CDN.', 'error');
        return;
    }

    // Cảnh báo nếu có nhiều phần tử #editor
    if (document.querySelectorAll('#editor').length !== 1) {
        console.warn('Phát hiện', document.querySelectorAll('#editor').length, 'phần tử #editor. Hãy đảm bảo chỉ có 1.');
    }

    ClassicEditor
        .create(document.querySelector('#editor'), {
            toolbar: {
                items: [
                    'heading', '|',
                    'bold', 'italic', 'underline', 'strikethrough', '|',
                    'fontSize', 'fontColor', 'fontBackgroundColor', '|',
                    'bulletedList', 'numberedList', '|',
                    'alignment', '|',
                    'link', 'insertTable', '|',
                    'undo', 'redo'
                ]
            },
            language: 'vi'
        })
        .then(function (editor) {
            editorInstance = editor;
            window.editorInstance = editor; // tiện debug trong console

            // Cache nội dung mỗi khi thay đổi
            try { latestEditorHtml = editor.getData(); } catch { }
            editor.model.document.on('change:data', function () {
                try { latestEditorHtml = editor.getData(); } catch (e) { console.warn('getData error:', e); }
            });

            // Debug helper
            window.dumpEditor = function () {
                try {
                    const d = editor.getData();
                    console.log('CKEditor getData() length:', d.length, '\n', d);
                    return d;
                } catch (e) {
                    console.error(e);
                    return '';
                }
            };

            // Enable the button now that editor is ready
            $('#createReport').prop('disabled', false);

            // Resolve readiness promise
            if (typeof editorReadyResolve === 'function') editorReadyResolve();
            console.log('CKEditor đã được khởi tạo');
        })
        .catch(function (error) {
            console.error('Lỗi khởi tạo CKEditor:', error);
            showNotification('Lỗi khởi tạo CKEditor', 'error');
        });
}

/**
 * Lấy nội dung từ CKEditor (CKEditor 5 v40+)
 */
function getEditorContent() {
    // 1) API chính thức
    if (editorInstance && typeof editorInstance.getData === 'function') {
        try { return editorInstance.getData(); } catch { }
    }
    // 2) Data controller (cũng hợp lệ)
    if (editorInstance && editorInstance.data && typeof editorInstance.data.get === 'function') {
        try { return editorInstance.data.get(); } catch { }
    }
    // 3) DOM editable (fallback)
    try {
        var domEditable = editorInstance?.ui?.view?.editable?.element;
        if (domEditable?.innerHTML) return domEditable.innerHTML;
    } catch { }
    // 4) Cache realtime
    if (latestEditorHtml) return latestEditorHtml;
    // 5) Fallback cuối
    var el = document.querySelector('#editor');
    return el ? el.innerHTML : '';
}

/**
 * Gửi HTML lên server để sinh PDF bằng Rotativa
 */
async function handleExportPDF() {
    try {
        // Đảm bảo editor đã sẵn sàng
        if (!editorInstance) await editorReady;

        const html = (getEditorContent() || '').trim();

        // Nếu vẫn rỗng, in chẩn đoán
        if (!html) {
            console.warn('getEditorContent() rỗng. Chẩn đoán:', {
                getData: (() => { try { return editorInstance?.getData() || ''; } catch { return ''; } })(),
                dataGet: (() => { try { return editorInstance?.data?.get() || ''; } catch { return ''; } })(),
                editableInner: (() => { try { return editorInstance?.ui?.view?.editable?.element?.innerHTML || ''; } catch { return ''; } })(),
                cache: latestEditorHtml
            });
        }

        if (!html || html === '<p><br></p>') {
            showNotification('Vui lòng nhập nội dung báo cáo!', 'warning');
            return;
        }

        showLoading('Đang tạo PDF...');

        const res = await fetch('/Report/UploadReport', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ html })
        });

        if (!res.ok) {
            const msg = await res.text();
            throw new Error(msg || 'Tạo PDF thất bại');
        }

        const blob = await res.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `BaoCaoTuan_${Date.now()}.pdf`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);

        showNotification('Tạo PDF thành công!', 'success');
    } catch (e) {
        console.error(e);
        showNotification('Lỗi tạo PDF: ' + (e.message || e), 'error');
    } finally {
        hideLoading();
    }
}

/**
 * Set nội dung cho CKEditor
 */
function setEditorContent(content) {
    if (editorInstance) {
        editorInstance.setData(content || '');
        latestEditorHtml = content || '';
    } else {
        var el = document.querySelector('#editor');
        if (el) el.innerHTML = content || '';
        latestEditorHtml = content || '';
    }
}

/**
 * Hiển thị thông báo
 */
function showNotification(message, type) {
    var bgColor = '#4f46e5';
    var icon = 'fa-info-circle';

    switch (type) {
        case 'success': bgColor = '#10b981'; icon = 'fa-check-circle'; break;
        case 'warning': bgColor = '#f59e0b'; icon = 'fa-exclamation-triangle'; break;
        case 'error': bgColor = '#ef4444'; icon = 'fa-times-circle'; break;
    }

    var notification = $('<div class="custom-notification"></div>');
    notification.html('<i class="fas ' + icon + '"></i><span>' + message + '</span>');
    notification.css({
        'position': 'fixed',
        'top': '20px',
        'right': '20px',
        'background': bgColor,
        'color': '#fff',
        'padding': '15px 25px',
        'border-radius': '8px',
        'box-shadow': '0 4px 12px rgba(0,0,0,0.15)',
        'z-index': '9999',
        'display': 'flex',
        'align-items': 'center',
        'gap': '10px',
        'font-weight': '600',
        'animation': 'slideInRight 0.3s ease'
    });

    $('body').append(notification);

    setTimeout(function () {
        notification.fadeOut(300, function () { $(this).remove(); });
    }, 3000);
}

/**
 * Hiển thị loading
 */
function showLoading(message) {
    var loading = $('<div class="custom-loading"></div>');
    loading.html(
        '<div class="spinner-border text-light" role="status">' +
        '<span class="visually-hidden">Loading...</span>' +
        '</div>' +
        '<p>' + (message || 'Đang xử lý...') + '</p>'
    );
    loading.css({
        'position': 'fixed',
        'top': '0',
        'left': '0',
        'width': '100%',
        'height': '100%',
        'background': 'rgba(0,0,0,0.7)',
        'display': 'flex',
        'flex-direction': 'column',
        'align-items': 'center',
        'justify-content': 'center',
        'gap': '15px',
        'z-index': '99999',
        'color': '#fff'
    });

    $('body').append(loading);
}

/**
 * Ẩn loading
 */
function hideLoading() {
    $('.custom-loading').fadeOut(300, function () { $(this).remove(); });
}