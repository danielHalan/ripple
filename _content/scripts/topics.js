$(document).ready(function () {

    var sections = $('section');
    if (sections.size() == 0) {
        $('.sidebar h3.half-margin').hide();
    }
    else {
        $('section').each(function(i, section) {
            var id = section.id;
            var heading = $('.section-header', section).text();

            $('<li><a href="#' + id + '">' + heading + '</a>').appendTo("#page-toc");
        });
    }

    $('#page-toc').affix();

    $('a.edit-link').click(function (e) {
        $.ajax({
            type: "POST",
            url: $(this).data('url'),
            data: {Key: $(this).data('key')},
            dataType: 'json'
        });

        e.preventDefault();
        return false;
    });
});