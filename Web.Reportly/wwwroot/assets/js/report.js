
var editorInstance = null;
var editorReadyResolve = null;
var editorReady = new Promise(resolve => editorReadyResolve = resolve);

$(function () {
    initCKEditor();
    $('#createReport').on('click', handleCreateReportDraft);
    console.log('Report.js initialized');
});

/**
 * Khởi tạo CKEditor 5
 */
function initCKEditor() {
    if (typeof ClassicEditor === 'undefined') return;

    ClassicEditor
        .create(document.querySelector('#editor'), {
            language: 'vi',
            placeholder: 'Nhập nội dung báo cáo tại đây...',
        })
        .then(editor => {
            editorInstance = editor;
            $('#createReport').prop('disabled', false);
            if (editorReadyResolve) editorReadyResolve();
        })
        .catch(error => {
            console.error('CKEditor init error:', error);
            if(window.AppUtils && window.AppUtils.showToast) {
                window.AppUtils.showToast('Lỗi khởi tạo trình soạn thảo', 'error');
            }
        });
}

/**
 * Xử lý tạo báo cáo nháp (Draft) và QR Code
 */
async function handleCreateReportDraft() {
    // Định nghĩa các hàm helper gọi qua AppUtils
    const showToast = window.AppUtils ? window.AppUtils.showToast : console.error;
    const showLoading = window.AppUtils ? window.AppUtils.showPageLoading : console.log;
    const hideLoading = window.AppUtils ? window.AppUtils.hidePageLoading : function(){};

    try {
        if (!editorInstance) await editorReady;
        const html = editorInstance.getData();

        if (!html || html.trim() === '') {
            showToast('Vui lòng nhập nội dung báo cáo!', 'warning');
            return;
        }

        showLoading('Đang tạo báo cáo & QR Code...');

        // GỌI AJAX BẰNG JQUERY $.AJAX()
        const data = await $.ajax({
            url: '/Report/CreateReportDraft',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ html })
        });
        
        // Kiểm tra lỗi
        if (!data || !data.success) {
            throw new Error(data?.message || 'Tạo báo cáo thất bại.');
        }
        
        showToast(data.message, 'success');

        // Cập nhật giao diện bên tab "Nộp báo cáo" (submit.js)
        if (typeof window.updatePdfPreviewFromId === 'function') {
            window.updatePdfPreviewFromId(data.uploadId, data.fileName, data.filePath);
        }

        // Tự động tải file về máy user
        downloadFile(data.filePath, data.fileName);

    } catch (e) {
        // Xử lý lỗi
        let errorMessage = e.message || 'Lỗi không xác định.';
        if (e.responseJSON && e.responseJSON.message) {
             errorMessage = e.responseJSON.message;
        }
        console.error(e);
        showToast('Lỗi: ' + errorMessage, 'error');
    } finally {
        hideLoading();
    }
}

/**
 * Helper tải file (hoạt động tốt với link công khai trả về)
 */
function downloadFile(url, fileName) {
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName || 'download';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
}